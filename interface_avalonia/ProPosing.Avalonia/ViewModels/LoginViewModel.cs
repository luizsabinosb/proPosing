using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ProPosing.Avalonia.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private const string ValidEmail    = "luizsabino2003@gmail.com";
    private const string ValidPassword = "admin";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string _email = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string _password = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private bool _isLoading;

    public string LoginButtonText => IsLoading ? "ENTRANDO..." : "ENTRAR";

    // The error describes a past attempt; once the user edits a field it no
    // longer reflects current state, so clear it.
    partial void OnEmailChanged(string value) => ErrorMessage = null;
    partial void OnPasswordChanged(string value) => ErrorMessage = null;

    public event Action? LoginSucceeded;

    private bool CanLogin() =>
        !string.IsNullOrWhiteSpace(Email) &&
        !string.IsNullOrWhiteSpace(Password) &&
        !IsLoading;

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync()
    {
        IsLoading    = true;
        ErrorMessage = null;
        OnPropertyChanged(nameof(LoginButtonText));

        await Task.Delay(350);

        if (Email.Trim().Equals(ValidEmail, StringComparison.OrdinalIgnoreCase) &&
            Password == ValidPassword)
        {
            LoginSucceeded?.Invoke();
        }
        else
        {
            ErrorMessage = "Email ou senha inválidos.";
        }

        IsLoading = false;
        OnPropertyChanged(nameof(LoginButtonText));
    }
}
