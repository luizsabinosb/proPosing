using OpenCvSharp;
using ProPosing.Avalonia.Models;
using System.Runtime.InteropServices;

namespace ProPosing.Avalonia.Services;

public sealed class CameraPipelineService
{
    private static readonly (int A, int B)[] Connections =
    [
        (11, 12), (11, 23), (12, 24), (23, 24),
        (11, 13), (13, 15), (12, 14), (14, 16),
        (23, 25), (25, 27), (24, 26), (26, 28)
    ];

    private readonly AppConfig _config;
    private readonly PoseApiClient _apiClient;
    private readonly SemaphoreSlim _inferenceGate = new(1, 1);
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private PoseEvaluateResponse? _lastEvaluation;
    private string _poseMode = "enquadramento";
    private volatile bool _inferenceInFlight;

    public event Action<PipelineUpdate>? FrameReady;
    public event Action<string>? Error;

    public CameraPipelineService(AppConfig config, PoseApiClient apiClient)
    {
        _config = config;
        _apiClient = apiClient;
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
        var tick = 0;
        var fpsCounter = 0;
        var fps = 0;
        var fpsStart = DateTime.UtcNow;

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

            if (tick % Math.Max(1, _config.InferenceStride) == 0)
            {
                _ = TryInferAsync(frame.Clone(), _poseMode, cancellationToken);
            }

            using var preview = frame.Clone();
            if (_lastEvaluation is not null && _lastEvaluation.Landmarks.Count > 0)
            {
                DrawOverlay(preview, _lastEvaluation);
            }

            using var bgra = new Mat();
            Cv2.CvtColor(preview, bgra, ColorConversionCodes.BGR2BGRA);
            var buffer = new byte[bgra.Rows * bgra.Cols * bgra.ElemSize()];
            System.Runtime.InteropServices.Marshal.Copy(bgra.Data, buffer, 0, buffer.Length);

            FrameReady?.Invoke(new PipelineUpdate
            {
                BgraBuffer = buffer,
                Width = bgra.Cols,
                Height = bgra.Rows,
                Fps = fps,
                Evaluation = _lastEvaluation,
                PoseMode = _poseMode,
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

    private async Task TryInferAsync(Mat frame, string poseMode, CancellationToken cancellationToken)
    {
        if (_inferenceInFlight)
        {
            frame.Dispose();
            return;
        }

        _inferenceInFlight = true;
        var gateAcquired = false;
        try
        {
            await _inferenceGate.WaitAsync(cancellationToken);
            gateAcquired = true;

            Cv2.ImEncode(".jpg", frame, out var imageBytes, [new ImageEncodingParam(ImwriteFlags.JpegQuality, 75)]);
            var imageB64 = Convert.ToBase64String(imageBytes);
            var response = await _apiClient.EvaluatePoseAsync(imageB64, poseMode, cancellationToken: cancellationToken);
            _lastEvaluation = SmoothEvaluation(_lastEvaluation, response);
        }
        catch (OperationCanceledException)
        {
            // Encerramento normal.
        }
        catch (Exception ex)
        {
            Error?.Invoke($"Erro de inferência: {ex.Message}");
        }
        finally
        {
            frame.Dispose();
            _inferenceInFlight = false;
            if (gateAcquired)
            {
                _inferenceGate.Release();
            }
        }
    }

    private static PoseEvaluateResponse SmoothEvaluation(
        PoseEvaluateResponse? previous,
        PoseEvaluateResponse current,
        double alpha = 0.45)
    {
        if (previous is null || previous.Landmarks.Count != current.Landmarks.Count || current.Landmarks.Count == 0)
        {
            return current;
        }

        for (var i = 0; i < current.Landmarks.Count; i++)
        {
            var p = previous.Landmarks[i];
            var n = current.Landmarks[i];
            n.X = p.X + ((n.X - p.X) * alpha);
            n.Y = p.Y + ((n.Y - p.Y) * alpha);
            n.Z = p.Z + ((n.Z - p.Z) * alpha);
            n.Visibility ??= p.Visibility;
        }

        return current;
    }

    private static void DrawOverlay(Mat frame, PoseEvaluateResponse evaluation)
    {
        var points = new List<Point?>(evaluation.Landmarks.Count);
        foreach (var lm in evaluation.Landmarks)
        {
            var visible = lm.Visibility is null || lm.Visibility > 0.5;
            if (!visible)
            {
                points.Add(null);
                continue;
            }

            var x = (int)(Math.Clamp(lm.X, 0, 1) * frame.Width);
            var y = (int)(Math.Clamp(lm.Y, 0, 1) * frame.Height);
            points.Add(new Point(x, y));
        }

        var color = evaluation.Status switch
        {
            "correct" => new Scalar(16, 185, 129),
            "adjustment_needed" => new Scalar(11, 193, 245),
            "incorrect" => new Scalar(68, 68, 239),
            _ => new Scalar(68, 68, 239),
        };

        foreach (var (a, b) in Connections)
        {
            if (a < points.Count && b < points.Count && points[a] is Point p1 && points[b] is Point p2)
            {
                Cv2.Line(frame, p1, p2, color, 2, LineTypes.AntiAlias);
            }
        }

        foreach (var idx in new[] { 11, 12, 13, 14, 15, 16, 23, 24, 25, 26 })
        {
            if (idx < points.Count && points[idx] is Point p)
            {
                Cv2.Circle(frame, p, 5, color, -1, LineTypes.AntiAlias);
                Cv2.Circle(frame, p, 2, new Scalar(255, 255, 255), -1, LineTypes.AntiAlias);
            }
        }
    }
}
