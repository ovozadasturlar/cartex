using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class MenuItem(string key, string icon, Func<ViewModelBase> factory)
{
    public string Key { get; } = key;
    public string Icon { get; } = icon;
    public Func<ViewModelBase> Factory { get; } = factory;
    public string Label => LocalizationManager.Instance[Key];
}

public partial class MainViewModel : ViewModelBase
{
    private readonly AuthService _authService;
    private readonly NavigationService _navigationService;

    [ObservableProperty]
    private ViewModelBase? _currentPage;

    [ObservableProperty]
    private MenuItem? _selectedMenuItem;

    [ObservableProperty]
    private bool _isTouchMode;

    [ObservableProperty]
    private string _currentTheme;

    [ObservableProperty]
    private string _currentLanguage;

    [ObservableProperty]
    private string _userDisplayName = string.Empty;

    [ObservableProperty]
    private string _userRole = string.Empty;

    public ObservableCollection<MenuItem> MenuItems { get; } = [];
    public string[] AvailableLanguages => LocalizationManager.AvailableLanguages;

    public MainViewModel(AuthService authService, NavigationService navigationService)
    {
        _authService = authService;
        _navigationService = navigationService;
        _isTouchMode = SettingsService.Instance.IsTouchMode;
        _currentTheme = SettingsService.Instance.Theme;
        _currentLanguage = SettingsService.Instance.Language;
    }

    public void Initialize()
    {
        UserDisplayName = _authService.UserInfo?.FullName ?? _authService.UserInfo?.Username ?? "";
        UserRole = _authService.UserInfo?.Role ?? "";

        MenuItems.Clear();
        MenuItems.Add(new MenuItem("dashboard", "📊", () => ServiceLocator.Resolve<DashboardViewModel>()));
        MenuItems.Add(new MenuItem("products", "📦", () => ServiceLocator.Resolve<ProductsViewModel>()));
        MenuItems.Add(new MenuItem("sales", "🛒", () => ServiceLocator.Resolve<SalesViewModel>()));
        MenuItems.Add(new MenuItem("customers", "👥", () => ServiceLocator.Resolve<CustomersViewModel>()));
        MenuItems.Add(new MenuItem("warehouse", "🏭", () => ServiceLocator.Resolve<WarehouseViewModel>()));

        if (_authService.HasPermission("users.view") || _authService.UserInfo?.Role == "Admin")
            MenuItems.Add(new MenuItem("users", "👤", () => ServiceLocator.Resolve<UsersViewModel>()));

        if (_authService.HasPermission("roles.view") || _authService.UserInfo?.Role == "Admin")
            MenuItems.Add(new MenuItem("roles", "🔑", () => ServiceLocator.Resolve<RolesViewModel>()));

        MenuItems.Add(new MenuItem("settings", "⚙️", () => ServiceLocator.Resolve<SettingsViewModel>()));

        if (MenuItems.Count > 0)
        {
            SelectedMenuItem = MenuItems[0];
        }
    }

    partial void OnSelectedMenuItemChanged(MenuItem? value)
    {
        if (value is not null)
            CurrentPage = value.Factory();
    }

    partial void OnIsTouchModeChanged(bool value)
    {
        SettingsService.Instance.IsTouchMode = value;
    }

    partial void OnCurrentThemeChanged(string value)
    {
        SettingsService.Instance.Theme = value;
        ThemeManager.ApplyTheme?.Invoke(value);
    }

    partial void OnCurrentLanguageChanged(string value)
    {
        SettingsService.Instance.Language = value;
        L.CurrentLanguage = value;
        OnPropertyChanged(nameof(L));

        var selected = SelectedMenuItem;
        var items = MenuItems.ToList();
        MenuItems.Clear();
        foreach (var item in items)
            MenuItems.Add(item);

        if (selected is not null)
            SelectedMenuItem = MenuItems.FirstOrDefault(m => m.Key == selected.Key);
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        CurrentTheme = CurrentTheme == "Dark" ? "Light" : "Dark";
    }

    [RelayCommand]
    private void Logout()
    {
        _authService.Logout();
        var loginVm = ServiceLocator.Resolve<LoginViewModel>();
        _navigationService.NavigateTo(loginVm);
    }
}
