using OpenCvSharp;
using ProPosing.Avalonia.Evaluation;
using ProPosing.Avalonia.Models;
using System.Runtime.InteropServices;

namespace ProPosing.Avalonia.Services;

public sealed class CameraPipelineService
{
    private readonly AppConfig _config;
    private readonly MediaPipeSidecar _sidecar;
    private readonly PoseEvaluatorRegistry _evaluatorRegistry;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private string _poseMode = "enquadramento";
    private volatile bool _inferenceInFlight;
    private IReadOnlyList<LandmarkPoint> _lastLandmarks = [];
    private PoseFeedback _lastFeedback = PoseFeedback.NoDetection;

    /// <summary>Fired when the pipeline emits a rendered frame.</summary>
    public event Action<PipelineUpdate>? FrameReady;

    /// <summary>Fired on unrecoverable errors (camera denied, not found, etc.).</summary>
    public event Action<string>? Error;

    /// <summary>Fired with transient status messages while waiting for permission.</summary>
    public event Action<string>? StatusUpdate;

    public CameraPipelineService(AppConfig config, MediaPipeSidecar sidecar, PoseEvaluatorRegistry evaluatorRegistry)
    {
        _config = config;
        _sidecar = sidecar;
        _evaluatorRegistry = evaluatorRegistry;
    }

    public bool IsRunning => _loopTask is { IsCompleted: false };

    public void SetPoseMode(string poseMode) => _poseMode = poseMode;

    public Task StartAsync()
    {
        if (IsRunning) return Task.CompletedTask;
        _cts = new CancellationTokenSource();
        _loopTask = Task.Run(() => CaptureLoopAsync(_cts.Token), _cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (_cts is null) return;
        _cts.Cancel();
        if (_loopTask is not null)
        {
            try { await _loopTask; }
            catch { /* ignored on shutdown */ }
        }
        _cts.Dispose();
        _cts = null;
        _loopTask = null;
    }

    // ── Main capture loop ──────────────────────────────────────────────────────

    /// <summary>
    /// Consecutive failed reads before the capture is considered dead and
    /// reopened (~3 s at the 60 ms retry interval). Covers camera unplug,
    /// driver hiccups and another app stealing the device.
    /// </summary>
    private const int MaxConsecutiveReadFailures = 50;

    private async Task CaptureLoopAsync(CancellationToken ct)
    {
        // Kiosk resilience: when the capture session dies (camera unplugged,
        // driver reset), reopen instead of ending the pipeline.
        while (!ct.IsCancellationRequested)
        {
            var reopen = await RunCaptureSessionAsync(ct);
            if (!reopen) return; // cancelled, or error already fired

            _resolvedCameraIndex = -1; // device may come back on another index
            Console.Error.WriteLine("[watchdog] Camera stopped delivering frames — reopening...");
            StatusUpdate?.Invoke("Câmera desconectada. Reconectando...");
        }
    }

    /// <summary>Returns true when the camera went silent and should be reopened.</summary>
    private async Task<bool> RunCaptureSessionAsync(CancellationToken ct)
    {
        // Wait for camera + permission before entering the frame loop.
        using var capture = await OpenCameraWithPermissionWaitAsync(ct);
        if (capture is null) return false; // error already fired

        capture.Set(VideoCaptureProperties.FrameWidth,  1280);
        capture.Set(VideoCaptureProperties.FrameHeight, 720);

        using var frame = new Mat();
        var frameIntervalMs = Math.Max(10, (int)(1000.0 / Math.Max(1, _config.TargetFps)));
        var fpsCounter = 0;
        var fps = 0;
        var fpsStart = DateTime.UtcNow;
        var tick = 0;
        var consecutiveReadFailures = 0;

        while (!ct.IsCancellationRequested)
        {
            var startedAt = DateTime.UtcNow;

            if (!capture.Read(frame) || frame.Empty())
            {
                if (++consecutiveReadFailures >= MaxConsecutiveReadFailures)
                    return true;
                await Task.Delay(60, ct);
                continue;
            }

            consecutiveReadFailures = 0;
            tick++;
            fpsCounter++;

            // Roughly once a second, make sure the inference sidecar is alive.
            if (tick % 30 == 0)
                EnsureSidecarAlive(ct);

            if ((DateTime.UtcNow - fpsStart).TotalSeconds >= 1)
            {
                fps = fpsCounter;
                fpsCounter = 0;
                fpsStart = DateTime.UtcNow;
            }

            if (tick % Math.Max(1, _config.InferenceStride) == 0 && !_inferenceInFlight)
            {
                var small = new Mat();
                Cv2.Resize(frame, small, new OpenCvSharp.Size(frame.Cols / 2, frame.Rows / 2));
                // Aspect ratio is invariant under uniform resize — use the source frame's.
                double aspect = frame.Rows > 0 ? (double)frame.Cols / frame.Rows : 1.0;
                _ = RunInferenceAsync(small, aspect, ct);
            }

            using var bgra = new Mat();
            Cv2.CvtColor(frame, bgra, ColorConversionCodes.BGR2BGRA);
            var buffer = new byte[bgra.Rows * bgra.Cols * bgra.ElemSize()];
            Marshal.Copy(bgra.Data, buffer, 0, buffer.Length);

            FrameReady?.Invoke(new PipelineUpdate
            {
                BgraBuffer = buffer,
                Width      = bgra.Cols,
                Height     = bgra.Rows,
                Fps        = fps,
                Landmarks  = _lastLandmarks,
                Feedback   = _lastFeedback,
                PoseMode   = _poseMode,
            });

            var elapsedMs = (int)(DateTime.UtcNow - startedAt).TotalMilliseconds;
            var sleepMs   = frameIntervalMs - elapsedMs;
            if (sleepMs > 0) await Task.Delay(sleepMs, ct);
        }

        return false; // cancelled
    }

    // ── Sidecar watchdog ───────────────────────────────────────────────────────

    private int _sidecarRestartGate; // 0 = idle, 1 = restart in progress

    /// <summary>
    /// Restarts the sidecar in the background if its process died. Exponential
    /// backoff between attempts; never throws into the capture loop. The video
    /// feed keeps running while inference is down — GetLandmarksAsync returns []
    /// and the UI degrades to "no detection" instead of freezing.
    /// </summary>
    private void EnsureSidecarAlive(CancellationToken ct)
    {
        if (_sidecar.IsAlive) return;
        if (Interlocked.CompareExchange(ref _sidecarRestartGate, 1, 0) != 0) return;

        _ = Task.Run(async () =>
        {
            try
            {
                var delay = TimeSpan.FromSeconds(2);
                while (!ct.IsCancellationRequested && !_sidecar.IsAlive)
                {
                    Console.Error.WriteLine("[watchdog] Sidecar down — restarting...");
                    try
                    {
                        await _sidecar.RestartAsync();
                        Console.Error.WriteLine("[watchdog] Sidecar restarted.");
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[watchdog] Sidecar restart failed: {ex.Message}");
                        await Task.Delay(delay, ct);
                        delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 30));
                    }
                }
            }
            catch (OperationCanceledException) { /* shutdown */ }
            finally
            {
                Interlocked.Exchange(ref _sidecarRestartGate, 0);
            }
        }, CancellationToken.None);
    }

    // ── Camera open + permission wait ──────────────────────────────────────────

    /// <summary>
    /// Opens the camera and waits up to 45 s for the first valid frame.
    ///
    /// macOS quirk: when the user grants camera permission for the first time,
    /// AVFoundation does NOT activate the existing VideoCapture instance.
    /// The capture must be CLOSED and RE-OPENED to gain access.
    /// We handle this by running short polling cycles (~6 s each) and
    /// re-creating the VideoCapture on every cycle, so the moment permission
    /// is granted the next open succeeds immediately.
    /// </summary>
    private async Task<VideoCapture?> OpenCameraWithPermissionWaitAsync(CancellationToken ct)
    {
        const int totalTimeoutSeconds = 45;
        const int cycleSeconds        = 6;
        const int pollIntervalMs      = 300;

        var deadline = DateTime.UtcNow.AddSeconds(totalTimeoutSeconds);

        StatusUpdate?.Invoke("Aguardando permissão de câmera...\nResponda ao diálogo do sistema para continuar.");

        while (!ct.IsCancellationRequested)
        {
            // Re-open on every cycle — required after macOS first-time permission grant.
            var cap = OpenCameraRaw();

            if (cap is null)
            {
                Error?.Invoke(
                    "Nenhuma câmera detectada.\n" +
                    "Verifique se a webcam está conectada e tente novamente.");
                return null;
            }

            // Poll this instance for up to cycleSeconds.
            var cycleDeadline = DateTime.UtcNow.AddSeconds(cycleSeconds);
            while (!ct.IsCancellationRequested && DateTime.UtcNow < cycleDeadline)
            {
                using var testFrame = new Mat();
                if (cap.Read(testFrame) && !testFrame.Empty())
                {
                    StatusUpdate?.Invoke(string.Empty);
                    return cap;
                }
                await Task.Delay(pollIntervalMs, ct);
            }

            // No frame this cycle — dispose and try again with a fresh capture.
            cap.Dispose();

            if (DateTime.UtcNow >= deadline)
            {
                Error?.Invoke(BuildPermissionDeniedMessage());
                return null;
            }

            await Task.Delay(200, ct);
        }

        return null;
    }

    // Cached after the first successful open so re-open cycles (macOS permission
    // quirk) and app restarts within the session skip the full probe.
    private int _resolvedCameraIndex = -1;
    private VideoCaptureAPIs _resolvedBackend = VideoCaptureAPIs.ANY;

    /// <summary>Highest device index probed when the configured one fails.</summary>
    private const int MaxProbeIndex = 4;

    /// <summary>
    /// Capture backends in order of reliability for the current OS:
    ///   macOS   → AVFoundation (the only stable backend for webcams)
    ///   Windows → DirectShow (most compatible), then Media Foundation
    ///   Linux   → V4L2
    /// ANY is always the last fallback.
    /// </summary>
    private static VideoCaptureAPIs[] PreferredBackends()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return [VideoCaptureAPIs.AVFOUNDATION, VideoCaptureAPIs.ANY];
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return [VideoCaptureAPIs.DSHOW, VideoCaptureAPIs.MSMF, VideoCaptureAPIs.ANY];
        return [VideoCaptureAPIs.V4L2, VideoCaptureAPIs.ANY];
    }

    /// <summary>
    /// Device indices to try: the configured one first, then 0..MaxProbeIndex,
    /// so the app finds a working webcam even when CAMERA_INDEX is wrong for
    /// this machine (e.g. index 0 is a virtual camera on Windows).
    /// </summary>
    private IEnumerable<int> CandidateIndices()
    {
        yield return _config.CameraIndex;
        for (var i = 0; i <= MaxProbeIndex; i++)
            if (i != _config.CameraIndex)
                yield return i;
    }

    /// <summary>
    /// Opens the first camera that responds, probing indices and backends.
    /// Returns null when no device opens at all. Does not test for frames —
    /// the permission wait loop handles that.
    /// </summary>
    private VideoCapture? OpenCameraRaw()
    {
        // Fast path: reuse the (index, backend) pair that worked before.
        if (_resolvedCameraIndex >= 0)
        {
            var cached = new VideoCapture(_resolvedCameraIndex, _resolvedBackend);
            if (cached.IsOpened()) return cached;
            cached.Dispose();
            _resolvedCameraIndex = -1; // device unplugged — fall through to probe
        }

        foreach (var index in CandidateIndices())
        {
            foreach (var backend in PreferredBackends())
            {
                var cap = new VideoCapture(index, backend);
                if (cap.IsOpened())
                {
                    Console.Error.WriteLine($"[camera] Opened index={index} backend={backend}");
                    _resolvedCameraIndex = index;
                    _resolvedBackend = backend;
                    return cap;
                }
                cap.Dispose();
            }
        }

        return null;
    }

    /// <summary>Camera-permission instructions for the current OS.</summary>
    private static string BuildPermissionDeniedMessage()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "Acesso à câmera negado.\n\n" +
                   "Para liberar o acesso:\n" +
                   "Ajustes do Sistema → Privacidade e Segurança → Câmera\n" +
                   "Ative o acesso para ProPosing (ou Terminal) e reinicie o app.";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "Não foi possível ler imagens da câmera.\n\n" +
                   "Verifique:\n" +
                   "Configurações → Privacidade e segurança → Câmera\n" +
                   "Ative \"Permitir que aplicativos da área de trabalho acessem sua câmera\"\n" +
                   "e feche outros programas que possam estar usando a webcam.";

        return "Não foi possível ler imagens da câmera.\n" +
               "Verifique as permissões de câmera do sistema e se outro programa não está usando a webcam.";
    }

    // ── Inference ──────────────────────────────────────────────────────────────

    private readonly PoseFeedbackSmoother _smoother = new();

    private async Task RunInferenceAsync(Mat frame, double aspect, CancellationToken ct)
    {
        _inferenceInFlight = true;
        try
        {
            var landmarks = await _sidecar.GetLandmarksAsync(frame, ct);
            _lastLandmarks = landmarks;
            var raw = _evaluatorRegistry.Evaluate(_poseMode, landmarks, aspect);
            _lastFeedback  = _smoother.Apply(_poseMode, raw);
        }
        catch (OperationCanceledException) { /* shutdown */ }
        finally
        {
            frame.Dispose();
            _inferenceInFlight = false;
        }
    }
}
