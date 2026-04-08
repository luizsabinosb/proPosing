using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProPosing.Avalonia.Models;
using ProPosing.Avalonia.Services;

namespace ProPosing.Avalonia.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly AppConfig _config;
    private readonly PoseApiClient _apiClient;
    private readonly CameraPipelineService _cameraPipelineService;

    [ObservableProperty]
    private WriteableBitmap? _cameraFrame;

    [ObservableProperty]
    private string _cameraError = string.Empty;

    [ObservableProperty]
    private string _selectedPoseMode = "enquadramento";

    [ObservableProperty]
    private string _poseQuality = "Aguardando detecção...";

    [ObservableProperty]
    private string _status = "no_detection";

    [ObservableProperty]
    private int _fps;

    [ObservableProperty]
    private int _processingTimeMs;

    [ObservableProperty]
    private int _landmarksCount;

    [ObservableProperty]
    private int? _imageWidth;

    [ObservableProperty]
    private int? _imageHeight;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private IReadOnlyList<LandmarkPoint> _landmarks = [];

    public MainWindowViewModel(AppConfig config, PoseApiClient apiClient, CameraPipelineService cameraPipelineService)
    {
        _config = config;
        _apiClient = apiClient;
        _cameraPipelineService = cameraPipelineService;

        Poses = new ObservableCollection<PoseOption>
        {
            new() { Number = 1, Mode = "enquadramento", Label = "ENQUADRAMENTO" },
            new() { Number = 2, Mode = "double_biceps", Label = "DUPLO BICEPS" },
            new() { Number = 3, Mode = "side_chest", Label = "SIDE CHEST" },
            new() { Number = 4, Mode = "side_triceps", Label = "SIDE TRICEPS" },
            new() { Number = 5, Mode = "most_muscular", Label = "MOST MUSCULAR" },
        };

        _cameraPipelineService.FrameReady += OnFrameReady;
        _cameraPipelineService.Error += OnPipelineError;
    }

    public ObservableCollection<PoseOption> Poses { get; }

    public string BackendUrl => _config.ApiBaseUrl;
    public bool HasFrame => CameraFrame is not null;
    public bool ShowPlaceholder => !HasFrame;
    public bool HasCameraError => !string.IsNullOrWhiteSpace(CameraError);
    public IBrush FpsIndicatorBrush => Fps >= 28 ? Brush.Parse("#10B981") : Brush.Parse("#F59E0B");

    [RelayCommand]
    private async Task StartCameraAsync()
    {
        if (IsRunning)
        {
            return;
        }

        if (!await _apiClient.HealthAsync())
        {
            CameraError = $"Backend não respondeu em {_config.ApiBaseUrl}/health";
        }
        else
        {
            CameraError = string.Empty;
        }
        _cameraPipelineService.SetPoseMode(SelectedPoseMode);
        await _cameraPipelineService.StartAsync();
        IsRunning = true;
    }

    [RelayCommand]
    private async Task StopCameraAsync()
    {
        await _cameraPipelineService.StopAsync();
        IsRunning = false;
    }

    [RelayCommand]
    private async Task SelectPoseAsync(string mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return;
        }

        SelectedPoseMode = mode;
        _cameraPipelineService.SetPoseMode(mode);
        try
        {
            await _apiClient.SelectPoseAsync(mode);
        }
        catch
        {
            // Sync opcional com backend, sem quebrar UX local.
        }
    }

    [RelayCommand]
    private async Task SelectPoseByNumberAsync(int number)
    {
        var pose = Poses.FirstOrDefault(p => p.Number == number);
        if (pose is not null)
        {
            await SelectPoseAsync(pose.Mode);
        }
    }

    public async Task HandleKeyAsync(global::Avalonia.Input.Key key)
    {
        if (key is >= global::Avalonia.Input.Key.D1 and <= global::Avalonia.Input.Key.D5)
        {
            await SelectPoseByNumberAsync((int)key - (int)global::Avalonia.Input.Key.D0);
            return;
        }

        if (key is >= global::Avalonia.Input.Key.NumPad1 and <= global::Avalonia.Input.Key.NumPad5)
        {
            await SelectPoseByNumberAsync((int)key - (int)global::Avalonia.Input.Key.NumPad0);
        }
    }

    private void OnPipelineError(string message)
    {
        Dispatcher.UIThread.Post(() => CameraError = message);
    }

    private void OnFrameReady(PipelineUpdate update)
    {
        Dispatcher.UIThread.Post(() =>
        {
            CameraFrame = CreateBitmap(update.BgraBuffer, update.Width, update.Height);
            Fps = update.Fps;

            var evaluation = update.Evaluation;
            if (evaluation is null)
            {
                return;
            }

            Status = evaluation.Status;
            PoseQuality = string.IsNullOrWhiteSpace(evaluation.PoseQuality)
                ? DefaultMessageForStatus(evaluation.Status)
                : evaluation.PoseQuality;
            ProcessingTimeMs = evaluation.ProcessingTimeMs;
            LandmarksCount = evaluation.Landmarks.Count;
            Landmarks = evaluation.Landmarks;
            ImageWidth = evaluation.ImageWidth ?? update.Width;
            ImageHeight = evaluation.ImageHeight ?? update.Height;
        });
    }

    private static WriteableBitmap CreateBitmap(byte[] bgraBuffer, int width, int height)
    {
        var bitmap = new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Unpremul);

        using var locked = bitmap.Lock();
        Marshal.Copy(bgraBuffer, 0, locked.Address, bgraBuffer.Length);
        return bitmap;
    }

    private static string DefaultMessageForStatus(string status)
    {
        return status switch
        {
            "correct" => "Posição correta.",
            "incorrect" => "Posição incorreta. Ajuste necessário.",
            "adjustment_needed" => "Ajuste fino necessário.",
            _ => "Aguardando detecção...",
        };
    }

    partial void OnCameraFrameChanged(WriteableBitmap? value)
    {
        OnPropertyChanged(nameof(HasFrame));
        OnPropertyChanged(nameof(ShowPlaceholder));
    }

    partial void OnCameraErrorChanged(string value)
    {
        OnPropertyChanged(nameof(HasCameraError));
    }

    partial void OnFpsChanged(int value)
    {
        OnPropertyChanged(nameof(FpsIndicatorBrush));
    }
}
