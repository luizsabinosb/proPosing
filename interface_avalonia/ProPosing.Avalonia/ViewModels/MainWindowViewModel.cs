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
using System;

namespace ProPosing.Avalonia.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly AppConfig _config;
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
    private int _landmarksCount;

    [ObservableProperty]
    private int? _imageWidth;

    [ObservableProperty]
    private int? _imageHeight;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private Bitmap? _referenceImage;

    public bool HasReferenceImage => ReferenceImage is not null;

    [ObservableProperty]
    private IReadOnlyList<LandmarkPoint> _landmarks = [];

    [ObservableProperty]
    private IReadOnlyList<string> _hints = [];

    public MainWindowViewModel(AppConfig config, CameraPipelineService cameraPipelineService)
    {
        _config = config;
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

    public bool HasFrame => CameraFrame is not null;
    public bool ShowPlaceholder => !HasFrame;
    public bool HasCameraError => !string.IsNullOrWhiteSpace(CameraError);
    public IBrush FpsIndicatorBrush => Fps >= 28 ? Brush.Parse("#10B981") : Brush.Parse("#F59E0B");

    [RelayCommand]
    private async Task StartCameraAsync()
    {
        if (IsRunning) return;

        CameraError = string.Empty;
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
    private Task SelectPoseAsync(string mode)
    {
        if (string.IsNullOrWhiteSpace(mode)) return Task.CompletedTask;

        SelectedPoseMode = mode;
        _cameraPipelineService.SetPoseMode(mode);
        return Task.CompletedTask;
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

    public bool HasHints => Hints.Count > 0;

    public string SelectedPoseLabel =>
        Poses.FirstOrDefault(p => p.Mode == SelectedPoseMode)?.Label ?? SelectedPoseMode.ToUpperInvariant();

    // Prevents queuing more than one pending UI update at a time.
    private volatile bool _renderPending;

    private void OnFrameReady(PipelineUpdate update)
    {
        if (_renderPending) return;  // drop frame — UI still processing previous one
        _renderPending = true;

        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                CameraFrame    = CreateBitmap(update.BgraBuffer, update.Width, update.Height);
                Fps            = update.Fps;
                Landmarks      = update.Landmarks;
                LandmarksCount = update.Landmarks.Count;
                ImageWidth     = update.Width;
                ImageHeight    = update.Height;

                var fb = update.Feedback;
                Status      = fb?.Status   ?? "no_detection";
                PoseQuality = fb?.Message  ?? "Aguardando detecção...";
                Hints       = fb?.Hints    ?? [];
                OnPropertyChanged(nameof(HasHints));
            }
            finally
            {
                _renderPending = false;
            }
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

    partial void OnSelectedPoseModeChanged(string value)
    {
        ReferenceImage = LoadReferenceImage(value);
        OnPropertyChanged(nameof(HasReferenceImage));
        OnPropertyChanged(nameof(SelectedPoseLabel));
    }

    partial void OnReferenceImageChanged(Bitmap? value)
    {
        OnPropertyChanged(nameof(HasReferenceImage));
    }

    private static readonly Dictionary<string, string> _referenceImageFiles = new()
    {
        ["double_biceps"]  = "doubleBiceps.jpg",
        ["side_chest"]     = "sideChest.jpg",
        ["side_triceps"]   = "sideTriceps.png",
        ["most_muscular"]  = "mostMuscular.png",
    };

    private static Bitmap? LoadReferenceImage(string poseMode)
    {
        if (!_referenceImageFiles.TryGetValue(poseMode, out var filename))
            return null;

        try
        {
            var uri = new Uri($"avares://ProPosing.Avalonia/Assets/References/{filename}");
            using var stream = AssetLoader.Open(uri);
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }
}
