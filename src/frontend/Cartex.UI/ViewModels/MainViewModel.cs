using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.UI.Models;
using Cartex.UI.Services;
using Material.Icons;

namespace Cartex.UI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly AuthService _authService;
    private readonly NavigationService _navigationService;

    [ObservableProperty] private ViewModelBase? _currentPage;
    [ObservableProperty] private MenuItem? _selectedMenuItem;
    [ObservableProperty] private AppMode _currentMode;
    [ObservableProperty] private AppTheme _currentTheme;
    [ObservableProperty] private string _currentLanguage;
    [ObservableProperty] private string _userDisplayName = "";
    [ObservableProperty] private string _userRole = "";
    [ObservableProperty] private string _currentPageTitle = "";

    [ObservableProperty] private bool _isThemePopupOpen;
    [ObservableProperty] private bool _isModePopupOpen;
    [ObservableProperty] private bool _isLanguagePopupOpen;
    [ObservableProperty] private bool _isSidebarCollapsed;

    public string UserInitial => string.IsNullOrEmpty(UserDisplayName) ? "?" : UserDisplayName[..1].ToUpper();

    public bool IsTouchMode
    {
        get => CurrentMode == AppMode.Touch;
        set => CurrentMode = value ? AppMode.Touch : AppMode.Desktop;
    }

    public bool IsDarkTheme
    {
        get => CurrentTheme == AppTheme.Dark;
        set => CurrentTheme = value ? AppTheme.Dark : AppTheme.Light;
    }

    public MaterialIconKind ThemeIcon => CurrentTheme == AppTheme.Dark
        ? MaterialIconKind.WeatherNight
        : MaterialIconKind.WeatherSunny;

    public MaterialIconKind ModeIcon => CurrentMode == AppMode.Touch
        ? MaterialIconKind.GestureTap
        : MaterialIconKind.Monitor;

    public ObservableCollection<MenuItem> MenuItems { get; } = [];
    public string[] AvailableLanguages => LocalizationManager.AvailableLanguages;

    public MainViewModel(AuthService authService, NavigationService navigationService)
    {
        _authService = authService;
        _navigationService = navigationService;
        _currentMode = SettingsService.Instance.Mode;
        _currentTheme = SettingsService.Instance.Theme;
        _currentLanguage = SettingsService.Instance.Language;

        _navigationService.MenuNavigationRequested += OnMenuNavigationRequested;
    }

    public void Initialize()
    {
        UserDisplayName = _authService.UserInfo?.FullName ?? _authService.UserInfo?.Username ?? "";
        UserRole = _authService.UserInfo?.Role ?? "";
        OnPropertyChanged(nameof(UserInitial));

        TouchModeManager.Instance.Mode = CurrentMode;

        MenuItems.Clear();
        AddMenuItem("dashboard", MaterialIconKind.ViewDashboard, typeof(DashboardViewModel), null);
        AddMenuItem("pos", MaterialIconKind.CashRegister, typeof(SalesViewModel), "sales.create");
        AddMenuItem("products", MaterialIconKind.PackageVariantClosed, typeof(ProductsViewModel), "products.view");
        AddMenuItem("inventory", MaterialIconKind.Warehouse, typeof(WarehouseViewModel), "stocks.view");
        AddMenuItem("sales", MaterialIconKind.ChartLine, typeof(SalesHistoryViewModel), "sales.view");
        AddMenuItem("reports", MaterialIconKind.ChartBar, typeof(ReportsViewModel), "reports.view");
        AddMenuItem("settings", MaterialIconKind.Cog, typeof(SettingsViewModel), null);

        UpdateMenuTitles();

        if (MenuItems.Count > 0)
            SelectedMenuItem = MenuItems[0];
    }

    private void AddMenuItem(string key, MaterialIconKind icon, Type vmType, string? permission)
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

            _ = CurrentPage switch
            {
                DashboardViewModel vm => vm.LoadCommand.ExecuteAsync(null),
                ProductsViewModel vm => vm.LoadCommand.ExecuteAsync(null),
                SalesViewModel vm => vm.LoadStocksCommand.ExecuteAsync(null),
                SalesHistoryViewModel vm => vm.LoadCommand.ExecuteAsync(null),
                ReportsViewModel vm => vm.LoadCommand.ExecuteAsync(null),
                CustomersViewModel vm => vm.LoadCommand.ExecuteAsync(null),
                UsersViewModel vm => vm.LoadCommand.ExecuteAsync(null),
                RolesViewModel vm => vm.LoadCommand.ExecuteAsync(null),
                WarehouseViewModel vm => vm.LoadWarehousesCommand.ExecuteAsync(null),
                _ => Task.CompletedTask
            };
        }
    }

    partial void OnCurrentModeChanged(AppMode value)
    {
        SettingsService.Instance.Mode = value;
        TouchModeManager.Instance.Mode = value;
        OnPropertyChanged(nameof(IsTouchMode));
        OnPropertyChanged(nameof(ModeIcon));
    }

    partial void OnCurrentThemeChanged(AppTheme value)
    {
        SettingsService.Instance.Theme = value;
        ThemeManager.ApplyTheme?.Invoke(value);
        OnPropertyChanged(nameof(IsDarkTheme));
        OnPropertyChanged(nameof(ThemeIcon));
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
    private void ToggleThemePopup()
    {
        IsThemePopupOpen = !IsThemePopupOpen;
        IsModePopupOpen = false;
        IsLanguagePopupOpen = false;
    }

    [RelayCommand]
    private void ToggleModePopup()
    {
        IsModePopupOpen = !IsModePopupOpen;
        IsThemePopupOpen = false;
        IsLanguagePopupOpen = false;
    }

    [RelayCommand]
    private void ToggleLanguagePopup()
    {
        IsLanguagePopupOpen = !IsLanguagePopupOpen;
        IsThemePopupOpen = false;
        IsModePopupOpen = false;
    }

    [RelayCommand]
    private void SelectTheme(string theme)
    {
        CurrentTheme = Enum.Parse<AppTheme>(theme);
        IsThemePopupOpen = false;
    }

    [RelayCommand]
    private void SelectMode(string mode)
    {
        CurrentMode = Enum.Parse<AppMode>(mode);
        IsModePopupOpen = false;
    }

    [RelayCommand]
    private void SelectLanguage(string lang)
    {
        CurrentLanguage = lang;
        IsLanguagePopupOpen = false;
    }

    [RelayCommand]
    private void ToggleSidebar() => IsSidebarCollapsed = !IsSidebarCollapsed;

    [RelayCommand]
    private void SelectMenuItem(MenuItem item) => SelectedMenuItem = item;

    [RelayCommand]
    private void Logout()
    {
        _authService.Logout();
        _navigationService.NavigateTo(ServiceLocator.Resolve<LoginViewModel>());
    }
}
