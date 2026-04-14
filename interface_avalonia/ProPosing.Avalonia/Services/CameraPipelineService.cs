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

    public event Action<PipelineUpdate>? FrameReady;
    public event Action<string>? Error;

    public CameraPipelineService(AppConfig config, MediaPipeSidecar sidecar, PoseEvaluatorRegistry evaluatorRegistry)
    {
        _config = config;
        _sidecar = sidecar;
        _evaluatorRegistry = evaluatorRegistry;
    }

    public bool IsRunning => _loopTask is { IsCompleted: false };

    public void SetPoseMode(string poseMode)
    {
        _poseMode = poseMode;
    }

    public Task StartAsync()
    {
        if (IsRunning)
        {
            return Task.CompletedTask;
        }

        _cts = new CancellationTokenSource();
        _loopTask = Task.Run(() => CaptureLoopAsync(_cts.Token), _cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (_cts is null)
        {
            return;
        }

        _cts.Cancel();
        if (_loopTask is not null)
        {
            try
            {
                await _loopTask;
            }
            catch
            {
                // Ignorado durante encerramento.
            }
        }

        _cts.Dispose();
        _cts = null;
        _loopTask = null;
    }

    private async Task CaptureLoopAsync(CancellationToken cancellationToken)
    {
        using var capture = TryOpenCamera();
        if (!capture.IsOpened())
        {
            Error?.Invoke(
                "Não foi possível abrir a câmera. " +
                "No macOS, permita câmera para o aplicativo (ou Terminal, se rodar via dotnet run) e feche apps que já estejam usando a webcam.");
            return;
        }

        capture.Set(VideoCaptureProperties.FrameWidth, 1280);
        capture.Set(VideoCaptureProperties.FrameHeight, 720);

        using var frame = new Mat();
        var frameIntervalMs = Math.Max(10, (int)(1000.0 / Math.Max(1, _config.TargetFps)));
        var fpsCounter = 0;
        var fps = 0;
        var fpsStart = DateTime.UtcNow;
        var tick = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var startedAt = DateTime.UtcNow;

            if (!capture.Read(frame) || frame.Empty())
            {
                Error?.Invoke("Falha ao capturar frame da câmera.");
                await Task.Delay(60, cancellationToken);
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

            // Fire inference every N frames without blocking the display loop.
            // Uses a half-resolution copy so MediaPipe processes 4× fewer pixels.
            if (tick % Math.Max(1, _config.InferenceStride) == 0 && !_inferenceInFlight)
            {
                var small = new Mat();
                Cv2.Resize(frame, small, new OpenCvSharp.Size(frame.Cols / 2, frame.Rows / 2));
                _ = RunInferenceAsync(small, cancellationToken);
            }

            using var bgra = new Mat();
            Cv2.CvtColor(frame, bgra, ColorConversionCodes.BGR2BGRA);
            var buffer = new byte[bgra.Rows * bgra.Cols * bgra.ElemSize()];
            System.Runtime.InteropServices.Marshal.Copy(bgra.Data, buffer, 0, buffer.Length);

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
            var sleepMs = frameIntervalMs - elapsedMs;
            if (sleepMs > 0)
            {
                await Task.Delay(sleepMs, cancellationToken);
            }
        }
    }

    private VideoCapture TryOpenCamera()
    {
        // Em macOS, AVFoundation costuma ser o backend mais estável para webcam.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var avf = new VideoCapture(_config.CameraIndex, (VideoCaptureAPIs)1200);
            if (avf.IsOpened())
            {
                // Valida leitura real — macOS pode abrir mas negar frame se permissão negada.
                using var testFrame = new Mat();
                if (avf.Read(testFrame) && !testFrame.Empty())
                {
                    return avf;
                }
                avf.Dispose();
                // Conseguiu abrir mas não leu: quase certamente permissão negada no macOS.
                Error?.Invoke(
                    "Câmera aberta, mas sem frames. Acesse Ajustes do Sistema → Privacidade e Segurança → Câmera " +
                    "e permita o acesso para o Terminal (ou para o app). Em seguida, reinicie.");
                return new VideoCapture();
            }
            avf.Dispose();
        }

        var any = new VideoCapture(_config.CameraIndex, VideoCaptureAPIs.ANY);
        if (any.IsOpened())
        {
            using var testFrame = new Mat();
            if (any.Read(testFrame) && !testFrame.Empty())
            {
                return any;
            }
            any.Dispose();
            Error?.Invoke(
                "Câmera aberta, mas sem frames. Acesse Ajustes do Sistema → Privacidade e Segurança → Câmera " +
                "e permita o acesso para o Terminal (ou para o app). Em seguida, reinicie.");
            return new VideoCapture();
        }
        any.Dispose();

        // Retorna instância fechada para fluxo de erro uniforme.
        return new VideoCapture();
    }

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
