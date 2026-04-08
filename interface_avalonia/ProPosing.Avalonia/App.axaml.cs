using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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
            var apiClient = new PoseApiClient(appConfig);
            var cameraPipeline = new CameraPipelineService(appConfig, apiClient);
            var vm = new MainWindowViewModel(appConfig, apiClient, cameraPipeline);
            desktop.MainWindow = new MainWindow { DataContext = vm };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
