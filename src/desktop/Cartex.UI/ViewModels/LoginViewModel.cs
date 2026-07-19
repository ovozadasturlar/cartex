using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.Shared.Models.Auth;
using Cartex.UI.Models;
using Cartex.UI.Services;
using Material.Icons;

namespace Cartex.UI.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly AuthService _authService;
    private readonly NavigationService _navigationService;

    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _rememberMe;

    [ObservableProperty] private bool _isThemePopupOpen;
    [ObservableProperty] private bool _isLanguagePopupOpen;

    [ObservableProperty] private bool _isServerEditOpen;
    [ObservableProperty] private string _serverUrl = SettingsService.Instance.ApiBaseUrl;
    [ObservableProperty] private string? _serverStatus;

    [ObservableProperty] private bool _keyDetected;
    public ObservableCollection<KeyProfile> KeyProfiles { get; } = [];
    public bool HasMultipleKeys => KeyProfiles.Count > 1;

    public sealed record KeyProfile(string Username, string Serial, string Content);

    [ObservableProperty] private bool _isQrOpen;
    [ObservableProperty] private bool _qrAvailable;
    [ObservableProperty] private double _qrProgress = 100;
    [ObservableProperty] private Avalonia.Media.Imaging.Bitmap? _qrImage;
    private CancellationTokenSource? _qrCts;

    [RelayCommand]
    private void ToggleQr()
    {
        if (IsQrOpen) { CloseQr(); return; }
        IsQrOpen = true;
        _ = RunQrLoopAsync();
    }

    private void CloseQr()
    {
        _qrCts?.Cancel();
        _qrCts = null;
        IsQrOpen = false;
        QrImage = null;
    }

    private async Task RunQrLoopAsync()
    {
        var cts = _qrCts = new CancellationTokenSource();
        try
        {
            while (!cts.IsCancellationRequested && IsQrOpen)
            {
                QrLoginStartResponse start;
                try { start = await _authService.StartQrAsync(cts.Token); }
                catch (OperationCanceledException) { return; }
                catch { await Task.Delay(3000, cts.Token); continue; }

                QrImage = QrRenderer.Render($"cartexqr:{start.Code}");
                var lifetime = TimeSpan.FromSeconds(Math.Max(30, start.ExpiresInSeconds - 5));
                var issuedAt = DateTime.UtcNow;
                QrProgress = 100;
                while (!cts.IsCancellationRequested)
                {
                    await Task.Delay(1000, cts.Token);
                    var remaining = lifetime - (DateTime.UtcNow - issuedAt);
                    if (remaining <= TimeSpan.Zero) break;
                    QrProgress = remaining.TotalSeconds / lifetime.TotalSeconds * 100;

                    LoginResponse? response = null;
                    try { response = await _authService.TryQrPollAsync(start.Code, cts.Token); }
                    catch (OperationCanceledException) { return; }
                    catch { }
                    if (response is not null)
                    {
                        if (cts.IsCancellationRequested) return;
                        CloseQr();
                        StopDrivePolling();
                        var mainVm = ServiceLocator.Resolve<MainViewModel>();
                        mainVm.Initialize();
                        _navigationService.NavigateTo(mainVm);
                        return;
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    private CancellationTokenSource? _driveCts;

    private void StartDrivePolling()
    {
        var cts = _driveCts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                try { await Task.Delay(3000, cts.Token); } catch { return; }
                var keys = HardwareKeyReader.ScanForKeys();
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (cts.IsCancellationRequested) return;
                    if (keys.Count == KeyProfiles.Count &&
                        keys.All(k => KeyProfiles.Any(p => p.Serial == k.Serial && p.Content == k.Content)))
                        return;
                    KeyProfiles.Clear();
                    foreach (var k in keys)
                        KeyProfiles.Add(new KeyProfile(k.Username ?? "—", k.Serial, k.Content));
                    KeyDetected = KeyProfiles.Count > 0;
                    OnPropertyChanged(nameof(HasMultipleKeys));
                });
            }
        }, cts.Token);
    }

    private void StopDrivePolling()
    {
        _driveCts?.Cancel();
        _driveCts = null;
    }

    public AppTheme CurrentTheme
    {
        get => SettingsService.Instance.Theme;
        set
        {
            SettingsService.Instance.Theme = value;
            ThemeManager.Instance.Theme = value;
            OnPropertyChanged(nameof(CurrentTheme));
            OnPropertyChanged(nameof(ThemeIcon));
        }
    }

    public AppLanguage CurrentLanguage
    {
        get => LocalizationManager.Instance.CurrentLanguage;
        set
        {
            SettingsService.Instance.Language = value;
            LocalizationManager.Instance.CurrentLanguage = value;
            OnPropertyChanged(nameof(CurrentLanguage));
            OnPropertyChanged(nameof(CurrentLanguageFlag));
        }
    }

    public MaterialIconKind ThemeIcon => CurrentTheme == AppTheme.Dark
        ? MaterialIconKind.WeatherNight
        : MaterialIconKind.WeatherSunny;

    public string CurrentLanguageFlag => LocalizationManager.GetLanguageShortCode(CurrentLanguage);
    public AppLanguage[] AvailableLanguages => LocalizationManager.AvailableLanguages;

    public LoginViewModel(AuthService authService, NavigationService navigationService)
    {
        _authService = authService;
        _navigationService = navigationService;
        _rememberMe = SettingsService.Instance.RememberMe;
        _ = InitLoginMethodsAsync();
    }

    private async Task InitLoginMethodsAsync()
    {
        var methods = await _authService.GetLoginMethodsAsync();
        QrAvailable = methods.QrEnabled;
        if (!methods.KeyEnabled) return;
        await DetectKey();
        StartDrivePolling();
    }

    [RelayCommand]
    private async Task DetectKey()
    {
        var keys = await Task.Run(HardwareKeyReader.ScanForKeys);
        KeyProfiles.Clear();
        foreach (var k in keys)
            KeyProfiles.Add(new KeyProfile(k.Username ?? "—", k.Serial, k.Content));
        KeyDetected = KeyProfiles.Count > 0;
        OnPropertyChanged(nameof(HasMultipleKeys));
    }

    [RelayCommand]
    private async Task LoginWithKeyAsync(KeyProfile profile)
    {
        CloseQr();
        ErrorMessage = null;
        IsLoading = true;
        try
        {
            await _authService.LoginWithKeyAsync(profile.Content, profile.Serial);
            StopDrivePolling();
            var mainVm = ServiceLocator.Resolve<MainViewModel>();
            mainVm.Initialize();
            _navigationService.NavigateTo(mainVm);
        }
        catch (Exception ex)
        {
            HandleLoginError(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ToggleServerEdit()
    {
        ServerUrl = SettingsService.Instance.ApiBaseUrl;
        ServerStatus = null;
        IsServerEditOpen = !IsServerEditOpen;
    }

    [RelayCommand]
    private async Task SaveServerUrlAsync()
    {
        var url = ServerUrl.Trim().TrimEnd('/');
        if (url.Length == 0) return;
        if (!url.StartsWith("http://") && !url.StartsWith("https://")) url = "http://" + url;
        ServerUrl = url;
        ServerStatus = null;
        ErrorMessage = null;
        IsLoading = true;
        try
        {
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            (await http.GetAsync($"{url}/health")).EnsureSuccessStatusCode();
            SettingsService.Instance.ApiBaseUrl = url;
            ServerStatus = L["server_connected"];
            IsServerEditOpen = false;
            _ = InitLoginMethodsAsync();
        }
        catch
        {
            ErrorMessage = L["err_server_unreachable"];
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static bool IsConnectionError(Exception ex) =>
        ex is System.Net.Http.HttpRequestException or TaskCanceledException
        || ex.InnerException is System.Net.Http.HttpRequestException or System.Net.Sockets.SocketException;

    private void HandleLoginError(Exception ex)
    {
        if (IsConnectionError(ex))
        {
            ErrorMessage = L["err_server_unreachable"];
            ServerUrl = SettingsService.Instance.ApiBaseUrl;
            ServerStatus = null;
            IsServerEditOpen = true;
            return;
        }
        ErrorMessage = ex.Message.Contains("401") || ex.Message.Contains("Unauthorized")
            ? L["login_error"]
            : ex.Message;
    }

    [RelayCommand]
    private void ToggleThemePopup() { IsThemePopupOpen = !IsThemePopupOpen; IsLanguagePopupOpen = false; }

    [RelayCommand]
    private void ToggleLanguagePopup() { IsLanguagePopupOpen = !IsLanguagePopupOpen; IsThemePopupOpen = false; }

    [RelayCommand]
    private void SelectTheme(string theme) { CurrentTheme = Enum.Parse<AppTheme>(theme); IsThemePopupOpen = false; }

    [RelayCommand]
    private void SelectLanguage(AppLanguage lang) { CurrentLanguage = lang; IsLanguagePopupOpen = false; }

    [RelayCommand]
    private async Task LoginAsync()
    {
        CloseQr();
        ErrorMessage = null;
        IsLoading = true;

        try
        {
            SettingsService.Instance.RememberMe = RememberMe;
            await _authService.LoginAsync(Username, Password, RememberMe);
            StopDrivePolling();
            var mainVm = ServiceLocator.Resolve<MainViewModel>();
            mainVm.Initialize();
            _navigationService.NavigateTo(mainVm);
        }
        catch (Exception ex)
        {
            HandleLoginError(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }
}
