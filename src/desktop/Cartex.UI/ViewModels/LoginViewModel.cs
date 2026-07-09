using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    [ObservableProperty] private bool _keyDetected;
    private DetectedDrive? _detectedDrive;

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
        _ = DetectKey();
    }

    [RelayCommand]
    private async Task DetectKey()
    {
        _detectedDrive = await Task.Run(HardwareKeyReader.ScanForKey);
        KeyDetected = _detectedDrive is not null;
    }

    [RelayCommand]
    private async Task LoginWithKeyAsync()
    {
        if (_detectedDrive?.KeyContent is null) return;
        ErrorMessage = null;
        IsLoading = true;
        try
        {
            await _authService.LoginWithKeyAsync(_detectedDrive.KeyContent, _detectedDrive.Serial);
            var mainVm = ServiceLocator.Resolve<MainViewModel>();
            mainVm.Initialize();
            _navigationService.NavigateTo(mainVm);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message.Contains("401") || ex.Message.Contains("Unauthorized")
                ? L["login_error"]
                : ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
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
        ErrorMessage = null;
        IsLoading = true;

        try
        {
            SettingsService.Instance.RememberMe = RememberMe;
            await _authService.LoginAsync(Username, Password, RememberMe);
            var mainVm = ServiceLocator.Resolve<MainViewModel>();
            mainVm.Initialize();
            _navigationService.NavigateTo(mainVm);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message.Contains("401") || ex.Message.Contains("Unauthorized")
                ? L["login_error"]
                : ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
