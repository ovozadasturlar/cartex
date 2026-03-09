using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _currentTheme;

    [ObservableProperty]
    private string _selectedLanguage;

    [ObservableProperty]
    private bool _isTouchMode;

    [ObservableProperty]
    private string _apiUrl;

    public string[] AvailableLanguages => LocalizationManager.AvailableLanguages;

    public SettingsViewModel()
    {
        _currentTheme = SettingsService.Instance.Theme;
        _selectedLanguage = SettingsService.Instance.Language;
        _isTouchMode = SettingsService.Instance.IsTouchMode;
        _apiUrl = SettingsService.Instance.ApiBaseUrl;
    }

    partial void OnCurrentThemeChanged(string value)
    {
        SettingsService.Instance.Theme = value;
        ThemeManager.ApplyTheme?.Invoke(value);
    }

    partial void OnSelectedLanguageChanged(string value)
    {
        SettingsService.Instance.Language = value;
        L.CurrentLanguage = value;
        OnPropertyChanged(nameof(L));
    }

    partial void OnIsTouchModeChanged(bool value)
    {
        SettingsService.Instance.IsTouchMode = value;
    }

    [RelayCommand]
    private void SaveApiUrl()
    {
        SettingsService.Instance.ApiBaseUrl = ApiUrl;
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        CurrentTheme = CurrentTheme == "Dark" ? "Light" : "Dark";
    }

    public static string GetLanguageDisplay(string code) =>
        LocalizationManager.GetLanguageDisplayName(code);
}
