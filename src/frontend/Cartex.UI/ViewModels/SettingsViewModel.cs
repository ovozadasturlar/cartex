using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.UI.Models;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    [ObservableProperty] private string _apiUrl;
    [ObservableProperty] private AppTheme _selectedTheme;
    [ObservableProperty] private AppMode _selectedMode;
    [ObservableProperty] private string _selectedLanguage;

    private UsersViewModel? _usersVm;
    private CustomersViewModel? _customersVm;
    private RolesViewModel? _rolesVm;

    public UsersViewModel UsersVm => _usersVm ??= ServiceLocator.Resolve<UsersViewModel>();
    public CustomersViewModel CustomersVm => _customersVm ??= ServiceLocator.Resolve<CustomersViewModel>();
    public RolesViewModel RolesVm => _rolesVm ??= ServiceLocator.Resolve<RolesViewModel>();

    public AppTheme[] AvailableThemes => Enum.GetValues<AppTheme>();
    public AppMode[] AvailableModes => Enum.GetValues<AppMode>();
    public string[] AvailableLanguages => LocalizationManager.AvailableLanguages;

    public SettingsViewModel()
    {
        _apiUrl = SettingsService.Instance.ApiBaseUrl;
        _selectedTheme = SettingsService.Instance.Theme;
        _selectedMode = SettingsService.Instance.Mode;
        _selectedLanguage = SettingsService.Instance.Language;
    }

    partial void OnSelectedThemeChanged(AppTheme value)
    {
        SettingsService.Instance.Theme = value;
        ThemeManager.ApplyTheme?.Invoke(value);
    }

    partial void OnSelectedModeChanged(AppMode value)
    {
        SettingsService.Instance.Mode = value;
        TouchModeManager.Instance.Mode = value;
    }

    partial void OnSelectedLanguageChanged(string value)
    {
        SettingsService.Instance.Language = value;
        LocalizationManager.Instance.CurrentLanguage = value;
    }

    [RelayCommand]
    private void SaveApiUrl() => SettingsService.Instance.ApiBaseUrl = ApiUrl;
}
