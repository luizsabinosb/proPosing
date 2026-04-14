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

    /// <summary>
    /// Resolves the sidecar script path: looks next to the running executable first,
    /// then falls back to the given override path.
    /// </summary>
    public static string ResolveSidecarPath()
    {
        // When built, MSBuild copies sidecar/ next to the binary.
        var nextToBinary = Path.Combine(AppContext.BaseDirectory, "sidecar", "mediapipe_sidecar.py");
        if (File.Exists(nextToBinary))
            return nextToBinary;

        // Fallback: relative to current working directory (useful during development).
        var fromCwd = Path.Combine("sidecar", "mediapipe_sidecar.py");
        if (File.Exists(fromCwd))
            return Path.GetFullPath(fromCwd);

        throw new FileNotFoundException(
            $"mediapipe_sidecar.py not found. Checked:\n  {nextToBinary}\n  {Path.GetFullPath(fromCwd)}");
    }

    /// <summary>
    /// Starts the sidecar process and waits until MediaPipe finishes loading.
    /// Must be awaited before calling GetLandmarksAsync.
    /// </summary>
    public async Task StartAsync(string pythonPath = "python3")
    {
        var scriptPath = ResolveSidecarPath();
        Console.Error.WriteLine($"[sidecar] Starting: {pythonPath} {scriptPath}");

        _process = new Process
        {
            StartInfo = new ProcessStartInfo(pythonPath, scriptPath)
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

    public void Dispose()
    {
        try { _stdin?.Close(); } catch { /* ignored */ }
        try { _stdout?.Close(); } catch { /* ignored */ }

        if (_process is not null)
        {
            try { _process.CancelErrorRead(); } catch { /* ignored */ }
            try { if (!_process.HasExited) _process.Kill(); } catch { /* ignored */ }
            _process.Dispose();
        }
    }
}
