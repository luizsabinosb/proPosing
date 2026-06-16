using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using OpenCvSharp;
using ProPosing.Avalonia.Models;

namespace ProPosing.Avalonia.Services;

/// <summary>
/// Spawns mediapipe_sidecar.py as a subprocess and communicates via stdin/stdout.
///
/// Protocol (stdin → sidecar):
///   12-byte header: [width: int32 LE] [height: int32 LE] [channels: int32 LE]
///   N bytes:        raw BGR pixel data  (width * height * channels)
///
/// Protocol (sidecar → stdout):
///   One JSON line per frame:
///   {"landmarks": [{"x":0.5,"y":0.3,"z":-0.1,"visibility":0.99}, ...]}
/// </summary>
public sealed class MediaPipeSidecar : IDisposable
{
    private Process? _process;
    private BinaryWriter? _stdin;
    private StreamReader? _stdout;
    private string _pythonPath = "python3";
    private volatile bool _starting;

    /// <summary>
    /// True while the sidecar is starting or its process is running and ready.
    /// The watchdog in CameraPipelineService polls this to trigger restarts.
    /// </summary>
    public bool IsAlive
    {
        get
        {
            if (_starting) return true;
            try
            {
                return _process is { HasExited: false } && _stdin is not null;
            }
            catch
            {
                return false; // process handle in an unqueryable state — treat as dead
            }
        }
    }

    /// <summary>Restarts the sidecar with the same python path used previously.</summary>
    public Task RestartAsync() => StartAsync(_pythonPath);

    /// <summary>
    /// Resolves how to launch the sidecar:
    ///   1. Compiled binary  (proposing-sidecar) next to the app — used in production .app
    ///   2. Python script    (sidecar/mediapipe_sidecar.py) next to the binary — dev build
    ///   3. Python script    relative to cwd — dotnet run fallback
    /// Returns (executable, arguments): when the compiled binary is used, arguments is "".
    /// </summary>
    private static (string executable, string arguments) ResolveSidecarInvocation(string pythonPath)
    {
        var baseDir = AppContext.BaseDirectory;

        // 1. Compiled sidecar binary — no Python needed (production bundle)
        // PyInstaller --onedir layout: proposing-sidecar/proposing-sidecar(.exe no Windows)
        var binName = OperatingSystem.IsWindows() ? "proposing-sidecar.exe" : "proposing-sidecar";
        var compiledBin = Path.Combine(baseDir, "proposing-sidecar", binName);
        if (File.Exists(compiledBin))
        {
            Console.Error.WriteLine($"[sidecar] Using compiled binary: {compiledBin}");
            return (compiledBin, "");
        }

        // 2. Python script next to the binary (dotnet publish / debug build)
        var scriptNextToBinary = Path.Combine(baseDir, "sidecar", "mediapipe_sidecar.py");
        if (File.Exists(scriptNextToBinary))
            return (pythonPath, scriptNextToBinary);

        // 3. Fallback: relative to current working directory (dotnet run from project root)
        var scriptFromCwd = Path.GetFullPath(Path.Combine("sidecar", "mediapipe_sidecar.py"));
        if (File.Exists(scriptFromCwd))
            return (pythonPath, scriptFromCwd);

        throw new FileNotFoundException(
            $"mediapipe sidecar not found. Checked:\n  {compiledBin}\n  {scriptNextToBinary}\n  {scriptFromCwd}");
    }

    /// <summary>
    /// Starts the sidecar process and waits until MediaPipe finishes loading.
    /// Uses the compiled binary (proposing-sidecar) when available; falls back to python3.
    /// Must be awaited before calling GetLandmarksAsync.
    /// </summary>
    public async Task StartAsync(string pythonPath = "python3")
    {
        _starting = true;
        try
        {
            await StartCoreAsync(pythonPath);
        }
        finally
        {
            _starting = false;
        }
    }

    private async Task StartCoreAsync(string pythonPath)
    {
        _pythonPath = pythonPath;

        // Hide the streams before tearing down so GetLandmarksAsync returns []
        // instead of touching a dying process.
        _stdin  = null;
        _stdout = null;
        ShutdownProcess();

        var (executable, arguments) = ResolveSidecarInvocation(pythonPath);
        Console.Error.WriteLine($"[sidecar] Starting: {executable} {arguments}".TrimEnd());

        _process = new Process
        {
            StartInfo = new ProcessStartInfo(executable, arguments)
            {
                RedirectStandardInput  = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
            }
        };

        _process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                Console.Error.WriteLine($"[python] {e.Data}");
        };

        _process.Start();
        _process.BeginErrorReadLine();

        // Use local variables until READY is confirmed — _stdin/_stdout stay null so
        // GetLandmarksAsync returns [] safely while Python is still initializing.
        var stdin  = new BinaryWriter(_process.StandardInput.BaseStream);
        var stdout = _process.StandardOutput;

        // Wait for "READY" — emitted by the sidecar after MediaPipe finishes loading.
        Console.Error.WriteLine("[sidecar] Waiting for MediaPipe to load...");
        while (true)
        {
            var line = await stdout.ReadLineAsync();
            if (line is null)
                throw new InvalidOperationException("Sidecar exited before sending READY.");
            if (line == "READY")
                break;
            // Any other line (e.g. "LOADING") is informational — ignore and keep waiting.
        }
        Console.Error.WriteLine("[sidecar] MediaPipe ready. Starting pipeline.");

        // Only expose streams to GetLandmarksAsync after READY is confirmed.
        _stdin  = stdin;
        _stdout = stdout;
    }

    /// <summary>
    /// Sends one BGR frame to the sidecar and waits for landmarks back.
    /// Returns empty list if no person is detected or on any error.
    /// NOT thread-safe — must be called from a single thread.
    /// </summary>
    public async Task<IReadOnlyList<LandmarkPoint>> GetLandmarksAsync(
        Mat frameBgr, CancellationToken ct)
    {
        if (_stdin is null || _stdout is null)
            return [];

        try
        {
            await SendFrameAsync(frameBgr, ct);
            return await ReadLandmarksAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return [];
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[sidecar] Error: {ex.Message}");
            return [];
        }
    }

    private async Task SendFrameAsync(Mat frameBgr, CancellationToken ct)
    {
        int w = frameBgr.Cols;
        int h = frameBgr.Rows;
        int c = frameBgr.ElemSize(); // always 3 for BGR

        // Header: 3 × int32 LE — built in a regular (non-async) method to avoid Span-in-async restriction.
        var header = BuildHeader(w, h, c);
        await _stdin!.BaseStream.WriteAsync(header, ct);

        // Pixel data
        int byteCount = w * h * c;
        var buf = ArrayPool<byte>.Shared.Rent(byteCount);
        try
        {
            System.Runtime.InteropServices.Marshal.Copy(frameBgr.Data, buf, 0, byteCount);
            await _stdin.BaseStream.WriteAsync(buf.AsMemory(0, byteCount), ct);
            await _stdin.BaseStream.FlushAsync(ct);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buf);
        }
    }

    private static byte[] BuildHeader(int w, int h, int c)
    {
        var header = new byte[12];
        BitConverter.TryWriteBytes(header.AsSpan(0, 4), w);
        BitConverter.TryWriteBytes(header.AsSpan(4, 4), h);
        BitConverter.TryWriteBytes(header.AsSpan(8, 4), c);
        return header;
    }

    private async Task<IReadOnlyList<LandmarkPoint>> ReadLandmarksAsync(CancellationToken ct)
    {
        var line = await _stdout!.ReadLineAsync(ct);
        if (string.IsNullOrEmpty(line))
            return [];

        using var doc = JsonDocument.Parse(line);
        var arr = doc.RootElement.GetProperty("landmarks");
        if (arr.GetArrayLength() == 0)
            return [];

        var result = new List<LandmarkPoint>(arr.GetArrayLength());
        foreach (var lm in arr.EnumerateArray())
        {
            result.Add(new LandmarkPoint
            {
                X          = lm.GetProperty("x").GetDouble(),
                Y          = lm.GetProperty("y").GetDouble(),
                Z          = lm.GetProperty("z").GetDouble(),
                Visibility = lm.GetProperty("visibility").GetDouble(),
            });
        }
        return result;
    }

    private void ShutdownProcess()
    {
        if (_process is null) return;
        try { _process.CancelErrorRead(); } catch { /* ignored */ }
        try { if (!_process.HasExited) _process.Kill(); } catch { /* ignored */ }
        try { _process.Dispose(); } catch { /* ignored */ }
        _process = null;
    }

    public void Dispose()
    {
        try { _stdin?.Close(); } catch { /* ignored */ }
        try { _stdout?.Close(); } catch { /* ignored */ }
        ShutdownProcess();
    }
}
