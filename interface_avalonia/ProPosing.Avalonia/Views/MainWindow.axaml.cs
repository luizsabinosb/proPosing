using Avalonia.Controls;
using Avalonia.Input;
using ProPosing.Avalonia.ViewModels;

namespace ProPosing.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
        Closing += OnClosing;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            await vm.StartCameraCommand.ExecuteAsync(null);
        }
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            await vm.StopCameraCommand.ExecuteAsync(null);
        }
    }

    private async void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        // Escape hatch for gym staff on kiosk machines; F11 re-enters fullscreen.
        if (e.Key == Key.Escape && WindowState == WindowState.FullScreen)
        {
            WindowState = WindowState.Normal;
            return;
        }
        if (e.Key == Key.F11)
        {
            WindowState = WindowState.FullScreen;
            return;
        }

        if (DataContext is MainWindowViewModel vm)
        {
            await vm.HandleKeyAsync(e.Key);
        }
    }
}
