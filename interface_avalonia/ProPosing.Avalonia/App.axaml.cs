using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ProPosing.Avalonia.Evaluation;
using ProPosing.Avalonia.Evaluation.Poses;
using static ProPosing.Avalonia.Evaluation.PoseThresholds;
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
            var appConfig  = AppConfig.LoadFromEnvironment();
            var thresholds = PoseThresholds.Load();
            var sidecar    = new MediaPipeSidecar();
            var registry   = new PoseEvaluatorRegistry([
                new EnquadramentoEvaluator(thresholds.Enquadramento),
                new DoubleBicepsEvaluator(thresholds.DoubleBiceps),
                new SideChestEvaluator(thresholds.SideChest),
                new SideTricepsEvaluator(thresholds.SideTriceps),
                new MostMuscularEvaluator(thresholds.MostMuscular),
                new QuarterTurnEvaluator(thresholds.QuarterTurn),
                new FrontLatSpreadEvaluator(thresholds.FrontLatSpread),
                new BackLatSpreadEvaluator(thresholds.BackLatSpread),
                new AbsAndThighsEvaluator(thresholds.AbsAndThighs),
                new TeaCupEvaluator(thresholds.TeaCup),
            ]);
            var cameraPipeline = new CameraPipelineService(appConfig, sidecar, registry);
            var mainVm         = new MainWindowViewModel(appConfig, cameraPipeline);

            // ── Pré-aquece o sidecar imediatamente — carrega em paralelo com o login ──
            // MediaPipe leva ~5-10s para inicializar; iniciando agora, estará pronto
            // (ou quase) quando o usuário terminar o login.
            _ = sidecar.StartAsync(appConfig.PythonPath).ContinueWith(t =>
            {
                if (t.IsFaulted)
                    Console.Error.WriteLine($"[sidecar] Failed to start: {t.Exception?.InnerException?.Message}");
            });

            if (appConfig.KioskMode)
            {
                // ── Kiosk (sala de poses, sem operador) ──────────────────
                // Sem login: a máquina precisa voltar sozinha após reboot.
                // Esc sai do fullscreen / F11 retorna (atalho da equipe).
                desktop.MainWindow = new MainWindow
                {
                    DataContext = mainVm,
                    WindowState = WindowState.FullScreen,
                };
            }
            else
            {
                // ── Login ────────────────────────────────────────────────
                var loginVm     = new LoginViewModel();
                var loginWindow = new LoginWindow { DataContext = loginVm };

                desktop.MainWindow = loginWindow;

                loginVm.LoginSucceeded += () =>
                {
                    var mainWindow = new MainWindow { DataContext = mainVm };

                    // Set new MainWindow before closing login to prevent app shutdown
                    desktop.MainWindow = mainWindow;
                    mainWindow.Show();
                    loginWindow.Close();
                };
            }

            desktop.ShutdownRequested += (_, _) => sidecar.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
