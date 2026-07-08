using System.Net;
using Cartex.Mobile.Agent.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Refit;

namespace Cartex.Mobile.Agent.ViewModels;

public partial class LoginViewModel(MobileAuthService auth, SessionStore session) : ObservableObject
{
    [ObservableProperty] private string _serverUrl = session.ServerUrl;
    [ObservableProperty] private string _username = "";
    [ObservableProperty] private string _password = "";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isServerVisible;
    [ObservableProperty] private string? _error;

    [RelayCommand]
    private void ToggleServer() => IsServerVisible = !IsServerVisible;

    public async Task InitializeAsync()
    {
        if (await auth.TryRestoreAsync())
            await Shell.Current.GoToAsync("//home");
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsBusy) return;
        Error = null;
        if (string.IsNullOrWhiteSpace(ServerUrl) || string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            Error = "Barcha maydonlarni to'ldiring";
            return;
        }
        if (!Uri.TryCreate(ServerUrl.Trim(), UriKind.Absolute, out _))
        {
            Error = "Server manzili noto'g'ri";
            return;
        }
        IsBusy = true;
        try
        {
            session.ServerUrl = ServerUrl.Trim();
            await auth.LoginAsync(Username.Trim(), Password);
            Password = "";
            await Shell.Current.GoToAsync("//home");
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            Error = "Login yoki parol noto'g'ri";
        }
        catch
        {
            Error = "Serverga ulanib bo'lmadi";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
