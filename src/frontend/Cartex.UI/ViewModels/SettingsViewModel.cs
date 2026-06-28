using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.UI.Models;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    [ObservableProperty] private string _apiUrl;
    [ObservableProperty] private AppLanguage _selectedLanguage;

    public AppLanguage[] AvailableLanguages => LocalizationManager.AvailableLanguages;

    public bool IsDarkTheme
    {
        get => SettingsService.Instance.Theme == AppTheme.Dark;
        set
        {
            var theme = value ? AppTheme.Dark : AppTheme.Light;
            SettingsService.Instance.Theme = theme;
            ThemeManager.Instance.Theme = theme;
            OnPropertyChanged();
        }
    }

    public SettingsViewModel()
    {
        _apiUrl = SettingsService.Instance.ApiBaseUrl;
        _selectedLanguage = SettingsService.Instance.Language;
    }

    partial void OnSelectedLanguageChanged(AppLanguage value)
    {
        SettingsService.Instance.Language = value;
        LocalizationManager.Instance.CurrentLanguage = value;
    }

    [RelayCommand]
    private void SaveApiUrl() => SettingsService.Instance.ApiBaseUrl = ApiUrl;
}
