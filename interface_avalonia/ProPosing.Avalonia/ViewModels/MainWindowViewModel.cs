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
    private string _cameraStatusMessage = string.Empty;

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

    [ObservableProperty]
    private double _referenceImageOpacity = 1.0;

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
            new() { Number = 0, Mode = "enquadramento",    Label = "ENQUADRAMENTO"   },
            new() { Number = 1, Mode = "double_biceps",    Label = "DUPLO BÍCEPS"    },
            new() { Number = 2, Mode = "side_chest",       Label = "SIDE CHEST"      },
            new() { Number = 3, Mode = "side_triceps",     Label = "SIDE TRÍCEPS"    },
            new() { Number = 4, Mode = "most_muscular",    Label = "MOST MUSCULAR"   },
            new() { Number = 5, Mode = "quarter_turn_side",Label = "QUARTER TURN"    },
            new() { Number = 6, Mode = "front_lat_spread", Label = "FRONT LAT SPREAD"},
            new() { Number = 7, Mode = "back_lat_spread",  Label = "BACK LAT SPREAD" },
            new() { Number = 8, Mode = "abs_and_thighs",   Label = "ABS AND THIGHS"  },
            new() { Number = 9, Mode = "teacup",           Label = "TEA CUP"         },
        };

        _cameraPipelineService.FrameReady      += OnFrameReady;
        _cameraPipelineService.Error           += OnPipelineError;
        _cameraPipelineService.StatusUpdate    += OnPipelineStatus;
    }

    public ObservableCollection<PoseOption> Poses { get; }

    public bool HasFrame => CameraFrame is not null;
    public bool ShowPlaceholder => !HasFrame;
    public bool HasCameraError => !string.IsNullOrWhiteSpace(CameraError);
    public bool HasCameraStatusMessage => !string.IsNullOrWhiteSpace(CameraStatusMessage);
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
        // Keys 0–9 → poses 0–9
        if (key is >= global::Avalonia.Input.Key.D0 and <= global::Avalonia.Input.Key.D9)
        {
            await SelectPoseByNumberAsync((int)key - (int)global::Avalonia.Input.Key.D0);
            return;
        }
        if (key is >= global::Avalonia.Input.Key.NumPad0 and <= global::Avalonia.Input.Key.NumPad9)
        {
            await SelectPoseByNumberAsync((int)key - (int)global::Avalonia.Input.Key.NumPad0);
        }
    }

    private void OnPipelineError(string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            CameraStatusMessage = string.Empty;
            CameraError = message;
            IsRunning   = false;
        });
    }

    private void OnPipelineStatus(string message)
    {
        Dispatcher.UIThread.Post(() => CameraStatusMessage = message);
    }

    public bool HasHints => Hints.Count > 0;

    /// <summary>
    /// True whenever there is a standalone pose quality message to display.
    /// This covers both the success message (from FromErrors with 0 errors)
    /// and direct early-return messages (evaluator guard clauses that bypass FromErrors).
    /// </summary>
    public bool HasPoseQualityMessage => !string.IsNullOrEmpty(PoseQuality);

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
                CameraStatusMessage = string.Empty;
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
                OnPropertyChanged(nameof(HasPoseQualityMessage));
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

    partial void OnCameraStatusMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasCameraStatusMessage));
    }

    partial void OnFpsChanged(int value)
    {
        OnPropertyChanged(nameof(FpsIndicatorBrush));
    }

    partial void OnSelectedPoseModeChanged(string value)
    {
        _ = TransitionReferenceImageAsync(value);
    }

    private async Task TransitionReferenceImageAsync(string poseMode)
    {
        // fade out
        ReferenceImageOpacity = 0.0;
        await Task.Delay(200);

        // troca conteúdo enquanto está invisível
        ReferenceImage = LoadReferenceImage(poseMode);
        OnPropertyChanged(nameof(HasReferenceImage));
        OnPropertyChanged(nameof(SelectedPoseLabel));

        // fade in
        ReferenceImageOpacity = 1.0;
    }

    partial void OnReferenceImageChanged(Bitmap? value)
    {
        OnPropertyChanged(nameof(HasReferenceImage));
    }

    private static readonly Dictionary<string, string> _referenceImageFiles = new()
    {
        ["double_biceps"]     = "doubleBiceps.jpg",
        ["side_chest"]        = "sideChest.jpg",
        ["side_triceps"]      = "sideTriceps.png",
        ["most_muscular"]     = "mostMuscular.png",
        ["quarter_turn_side"] = "quarterTurn.jpg",
        ["front_lat_spread"]  = "frontLatSpread.png",
        ["back_lat_spread"]   = "backLatSpread.jpg",
        ["abs_and_thighs"]    = "absAndThighs.png",
        ["teacup"]            = "teaCup.png",
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
