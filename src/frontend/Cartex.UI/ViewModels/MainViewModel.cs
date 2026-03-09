using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.UI.Models;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly AuthService _authService;
    private readonly NavigationService _navigationService;

    [ObservableProperty] private ViewModelBase? _currentPage;
    [ObservableProperty] private MenuItem? _selectedMenuItem;
    [ObservableProperty] private bool _isTouchMode;
    [ObservableProperty] private bool _isDarkTheme;
    [ObservableProperty] private string _currentLanguage;
    [ObservableProperty] private string _userDisplayName = "";
    [ObservableProperty] private string _userRole = "";
    [ObservableProperty] private string _currentPageTitle = "";

    public ObservableCollection<MenuItem> MenuItems { get; } = [];
    public string[] AvailableLanguages => LocalizationManager.AvailableLanguages;

    public MainViewModel(AuthService authService, NavigationService navigationService)
    {
        _authService = authService;
        _navigationService = navigationService;
        IsTouchMode = SettingsService.Instance.IsTouchMode;
        _isDarkTheme = SettingsService.Instance.Theme == "Dark";
        _currentLanguage = SettingsService.Instance.Language;

        _navigationService.MenuNavigationRequested += OnMenuNavigationRequested;
    }

    public void Initialize()
    {
        UserDisplayName = _authService.UserInfo?.FullName ?? _authService.UserInfo?.Username ?? "";
        UserRole = _authService.UserInfo?.Role ?? "";

        TouchModeManager.Instance.IsTouchMode = IsTouchMode;

        MenuItems.Clear();
        AddMenuItem("dashboard", "📊", typeof(DashboardViewModel), null);
        AddMenuItem("pos", "🛒", typeof(SalesViewModel), "sales.create");
        AddMenuItem("products", "📦", typeof(ProductsViewModel), "products.view");
        AddMenuItem("inventory", "🏭", typeof(WarehouseViewModel), "stocks.view");
        AddMenuItem("sales", "💰", typeof(SalesHistoryViewModel), "sales.view");
        AddMenuItem("reports", "📈", typeof(ReportsViewModel), "reports.view");
        AddMenuItem("settings", "⚙️", typeof(SettingsViewModel), null);

        UpdateMenuTitles();

        if (MenuItems.Count > 0)
            SelectedMenuItem = MenuItems[0];
    }

    private void AddMenuItem(string key, string icon, Type vmType, string? permission)
    {
        if (permission is not null && !_authService.HasPermission(permission) && _authService.UserInfo?.Role != "Admin")
            return;

        MenuItems.Add(new MenuItem { Key = key, Icon = icon, ViewModelType = vmType, Permission = permission });
    }

    private void UpdateMenuTitles()
    {
        foreach (var item in MenuItems)
            item.Title = L[item.Key];
    }

    private void OnMenuNavigationRequested(string menuKey)
    {
        var item = MenuItems.FirstOrDefault(m => m.Key == menuKey);
        if (item is not null)
            SelectedMenuItem = item;
    }

    partial void OnSelectedMenuItemChanged(MenuItem? oldValue, MenuItem? newValue)
    {
        if (oldValue is not null) oldValue.IsActive = false;
        if (newValue is not null)
        {
            newValue.IsActive = true;
            CurrentPage = (ViewModelBase)ServiceLocator.Resolve(newValue.ViewModelType);
            CurrentPageTitle = newValue.Title;
        }
    }

    partial void OnIsTouchModeChanged(bool value)
    {
        SettingsService.Instance.IsTouchMode = value;
        TouchModeManager.Instance.IsTouchMode = value;
    }

    partial void OnIsDarkThemeChanged(bool value)
    {
        var theme = value ? "Dark" : "Light";
        SettingsService.Instance.Theme = theme;
        ThemeManager.ApplyTheme?.Invoke(theme);
    }

    partial void OnCurrentLanguageChanged(string value)
    {
        SettingsService.Instance.Language = value;
        LocalizationManager.Instance.CurrentLanguage = value;
        UpdateMenuTitles();
        if (SelectedMenuItem is not null)
            CurrentPageTitle = SelectedMenuItem.Title;
    }

    [RelayCommand]
    private void ToggleTheme() => IsDarkTheme = !IsDarkTheme;

    [RelayCommand]
    private void SelectMenuItem(MenuItem item) => SelectedMenuItem = item;

    [RelayCommand]
    private void Logout()
    {
        _authService.Logout();
        _navigationService.NavigateTo(ServiceLocator.Resolve<LoginViewModel>());
    }
}
