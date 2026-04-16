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

    private async Task CaptureLoopAsync(CancellationToken ct)
    {
        // Wait for camera + permission before entering the frame loop.
        using var capture = await OpenCameraWithPermissionWaitAsync(ct);
        if (capture is null) return; // error already fired

        capture.Set(VideoCaptureProperties.FrameWidth,  1280);
        capture.Set(VideoCaptureProperties.FrameHeight, 720);

        using var frame = new Mat();
        var frameIntervalMs = Math.Max(10, (int)(1000.0 / Math.Max(1, _config.TargetFps)));
        var fpsCounter = 0;
        var fps = 0;
        var fpsStart = DateTime.UtcNow;
        var tick = 0;

        while (!ct.IsCancellationRequested)
        {
            var startedAt = DateTime.UtcNow;

            if (!capture.Read(frame) || frame.Empty())
            {
                await Task.Delay(60, ct);
                continue;
            }

            tick++;
            fpsCounter++;

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
                _ = RunInferenceAsync(small, ct);
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

            if (!cap.IsOpened())
            {
                cap.Dispose();
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
                Error?.Invoke(
                    "Acesso à câmera negado.\n\n" +
                    "Para liberar o acesso:\n" +
                    "Ajustes do Sistema → Privacidade e Segurança → Câmera\n" +
                    "Ative o acesso para ProPosing (ou Terminal) e reinicie o app.");
                return null;
            }

            await Task.Delay(200, ct);
        }

        return null;
    }

    /// <summary>Opens the camera device without testing for frames.</summary>
    private VideoCapture OpenCameraRaw()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // AVFoundation (1200) is the most stable backend for macOS webcams.
            var avf = new VideoCapture(_config.CameraIndex, (VideoCaptureAPIs)1200);
            if (avf.IsOpened()) return avf;
            avf.Dispose();
        }

        return new VideoCapture(_config.CameraIndex, VideoCaptureAPIs.ANY);
    }

    // ── Inference ──────────────────────────────────────────────────────────────

    private async Task RunInferenceAsync(Mat frame, CancellationToken ct)
    {
        _inferenceInFlight = true;
        try
        {
            var landmarks = await _sidecar.GetLandmarksAsync(frame, ct);
            _lastLandmarks = landmarks;
            _lastFeedback  = _evaluatorRegistry.Evaluate(_poseMode, landmarks);
        }
        catch (OperationCanceledException) { /* shutdown */ }
        finally
        {
            frame.Dispose();
            _inferenceInFlight = false;
        }
    }
}
