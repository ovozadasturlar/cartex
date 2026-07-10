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

    public string ServerText => $"{Loc.Instance["server"]}: {ServerUrl}";

    partial void OnServerUrlChanged(string value) => OnPropertyChanged(nameof(ServerText));

    [RelayCommand]
    private void ToggleServer() => IsServerVisible = !IsServerVisible;

    [RelayCommand]
    private async Task SetLanguageAsync(string code)
    {
        await Loc.Instance.SetLanguageAsync(code);
        Error = null;
        OnPropertyChanged(nameof(ServerText));
    }

    public async Task InitializeAsync()
    {
        if (!await auth.TryRestoreAsync()) return;
        if (AppLock.PinEnabled)
            await Shell.Current.GoToAsync("pin");
        else
            await Shell.Current.GoToAsync("//home");
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsBusy) return;
        Error = null;
        if (string.IsNullOrWhiteSpace(ServerUrl) || string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            Error = Loc.Instance["err_fill_all"];
            return;
        }
        if (!Uri.TryCreate(ServerUrl.Trim(), UriKind.Absolute, out _))
        {
            Error = Loc.Instance["err_bad_server"];
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
            Error = Loc.Instance["err_bad_credentials"];
        }
        catch
        {
            Error = Loc.Instance["err_no_connection"];
            IsServerVisible = true;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
