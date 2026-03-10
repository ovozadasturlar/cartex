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

    [ObservableProperty] private bool _isThemePopupOpen;
    [ObservableProperty] private bool _isModePopupOpen;
    [ObservableProperty] private bool _isLanguagePopupOpen;

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

    public AppMode CurrentMode
    {
        get => SettingsService.Instance.Mode;
        set
        {
            SettingsService.Instance.Mode = value;
            ModeManager.Instance.Mode = value;
            OnPropertyChanged(nameof(CurrentMode));
            OnPropertyChanged(nameof(ModeIcon));
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

    public MaterialIconKind ModeIcon => CurrentMode == AppMode.Touch
        ? MaterialIconKind.GestureTap
        : MaterialIconKind.Monitor;

    public string CurrentLanguageFlag => LocalizationManager.GetLanguageShortCode(CurrentLanguage);

    public AppLanguage[] AvailableLanguages => LocalizationManager.AvailableLanguages;

    public LoginViewModel(AuthService authService, NavigationService navigationService)
    {
        _authService = authService;
        _navigationService = navigationService;
    }

    [RelayCommand]
    private void ToggleThemePopup() { IsThemePopupOpen = !IsThemePopupOpen; IsModePopupOpen = false; IsLanguagePopupOpen = false; }

    [RelayCommand]
    private void ToggleModePopup() { IsModePopupOpen = !IsModePopupOpen; IsThemePopupOpen = false; IsLanguagePopupOpen = false; }

    [RelayCommand]
    private void ToggleLanguagePopup() { IsLanguagePopupOpen = !IsLanguagePopupOpen; IsThemePopupOpen = false; IsModePopupOpen = false; }

    [RelayCommand]
    private void SelectTheme(string theme) { CurrentTheme = Enum.Parse<AppTheme>(theme); IsThemePopupOpen = false; }

    [RelayCommand]
    private void SelectMode(string mode) { CurrentMode = Enum.Parse<AppMode>(mode); IsModePopupOpen = false; }

    [RelayCommand]
    private void SelectLanguage(AppLanguage lang) { CurrentLanguage = lang; IsLanguagePopupOpen = false; }

    [RelayCommand]
    private async Task LoginAsync()
    {
        ErrorMessage = null;
        IsLoading = true;

        try
        {
            await _authService.LoginAsync(Username, Password);
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
