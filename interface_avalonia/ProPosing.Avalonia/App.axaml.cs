using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ProPosing.Avalonia.Evaluation;
using ProPosing.Avalonia.Evaluation.Poses;
using ProPosing.Avalonia.Services;
using ProPosing.Avalonia.ViewModels;
using ProPosing.Avalonia.Views;

namespace ProPosing.Avalonia;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var appConfig = AppConfig.LoadFromEnvironment();
            var sidecar = new MediaPipeSidecar();
            var registry = new PoseEvaluatorRegistry([
                new EnquadramentoEvaluator(),
                new DoubleBicepsEvaluator(),
                new SideChestEvaluator(),
                new SideTricepsEvaluator(),
                new MostMuscularEvaluator(),
            ]);
            var cameraPipeline = new CameraPipelineService(appConfig, sidecar, registry);
            var vm = new MainWindowViewModel(appConfig, cameraPipeline);
            desktop.MainWindow = new MainWindow { DataContext = vm };

            // Start sidecar in background — window appears immediately while MediaPipe loads.
            // GetLandmarksAsync returns [] safely until the sidecar is ready.
            _ = sidecar.StartAsync(appConfig.PythonPath).ContinueWith(t =>
            {
                if (t.IsFaulted)
                    Console.Error.WriteLine($"[sidecar] Failed to start: {t.Exception?.InnerException?.Message}");
            });

            desktop.ShutdownRequested += (_, _) => sidecar.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
