using Avalonia.Controls;
using Avalonia.Input;
using ProPosing.Avalonia.ViewModels;

namespace ProPosing.Avalonia.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (DataContext is LoginViewModel vm && vm.LoginCommand.CanExecute(null))
            vm.LoginCommand.Execute(null);
    }
}
