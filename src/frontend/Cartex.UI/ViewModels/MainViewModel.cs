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
    private readonly Action _langChangedHandler;

    public BranchContextService Branch { get; }
    public IBusyService Busy { get; }

    [ObservableProperty] private ViewModelBase? _currentPage;
    [ObservableProperty] private MenuItem? _selectedMenuItem;
    [ObservableProperty] private AppTheme _currentTheme;
    [ObservableProperty] private AppLanguage _currentLanguage;
    [ObservableProperty] private string _userDisplayName = "";
    [ObservableProperty] private string _userRole = "";
    [ObservableProperty] private string _currentPageTitle = "";
    [ObservableProperty] private string _navFilter = "";

    [ObservableProperty] private bool _isThemePopupOpen;
    [ObservableProperty] private bool _isLanguagePopupOpen;
    [ObservableProperty] private bool _isUserMenuOpen;
    [ObservableProperty] private bool _isSidebarCollapsed;

    public string UserInitial => string.IsNullOrEmpty(UserDisplayName) ? "?" : UserDisplayName[..1].ToUpper();
    public bool IsDarkTheme { get => CurrentTheme == AppTheme.Dark; set => CurrentTheme = value ? AppTheme.Dark : AppTheme.Light; }
    public string CurrentLanguageFlag => LocalizationManager.GetLanguageShortCode(CurrentLanguage);
    public MaterialIconKind ThemeIcon => CurrentTheme == AppTheme.Dark ? MaterialIconKind.WeatherNight : MaterialIconKind.WeatherSunny;

    public ObservableCollection<MenuSection> MenuSections { get; } = [];
    public AppLanguage[] AvailableLanguages => LocalizationManager.AvailableLanguages;

    private record MenuDef(string SectionKey, string Key, MaterialIconKind Icon, Type VmType, string? Permission);

    private static readonly (string Key, string TitleKey)[] SectionDefs =
    [
        ("main", "section_main"),
        ("catalog", "section_catalog"),
        ("inventory", "section_inventory"),
        ("people", "section_people"),
        ("analytics", "section_analytics"),
        ("admin", "section_admin"),
    ];

    private static readonly MenuDef[] Defs =
    [
        new("main", "dashboard", MaterialIconKind.ViewDashboard, typeof(DashboardViewModel), null),
        new("main", "pos", MaterialIconKind.CashRegister, typeof(SalesViewModel), "sales.create"),
        new("catalog", "products", MaterialIconKind.PackageVariantClosed, typeof(ProductsViewModel), "products.view"),
        new("inventory", "inventory", MaterialIconKind.Warehouse, typeof(WarehouseViewModel), "stocks.view"),
        new("people", "customers", MaterialIconKind.AccountGroup, typeof(CustomersViewModel), "customers.view"),
        new("analytics", "sale_history", MaterialIconKind.ChartLine, typeof(SalesHistoryViewModel), "sales.view"),
        new("analytics", "reports", MaterialIconKind.ChartBar, typeof(ReportsViewModel), "reports.view"),
        new("admin", "users", MaterialIconKind.AccountCog, typeof(UsersViewModel), "users.view"),
        new("admin", "roles", MaterialIconKind.ShieldAccount, typeof(RolesViewModel), "roles.view"),
        new("admin", "settings", MaterialIconKind.Cog, typeof(SettingsViewModel), null),
    ];

    public MainViewModel(AuthService authService, NavigationService navigationService, BranchContextService branch, IBusyService busy)
    {
        _authService = authService;
        _navigationService = navigationService;
        Branch = branch;
        Busy = busy;
        _currentTheme = SettingsService.Instance.Theme;
        _currentLanguage = SettingsService.Instance.Language;

        _navigationService.MenuNavigationRequested += OnMenuNavigationRequested;
        ThemeManager.Instance.ThemeChanged += OnThemeManagedChanged;
        _langChangedHandler = OnLanguageManagedChanged;
        LocalizationManager.Instance.LanguageChanged += _langChangedHandler;
    }

    public void Initialize()
    {
        UserDisplayName = _authService.UserInfo?.FullName ?? _authService.UserInfo?.Username ?? "";
        UserRole = _authService.UserInfo?.Role ?? "";
        OnPropertyChanged(nameof(UserInitial));

        BuildMenu();
        _ = Branch.LoadAsync();

        SelectedMenuItem = MenuSections.SelectMany(s => s.Items).FirstOrDefault();
    }

    private void BuildMenu()
    {
        MenuSections.Clear();
        var isAdmin = _authService.UserInfo?.Role == "Admin";

        foreach (var (key, titleKey) in SectionDefs)
        {
            var section = new MenuSection { Key = key, Title = L[titleKey] };
            foreach (var def in Defs.Where(d => d.SectionKey == key))
            {
                if (def.Permission is not null && !_authService.HasPermission(def.Permission) && !isAdmin)
                    continue;
                section.Items.Add(new MenuItem
                {
                    Key = def.Key,
                    Icon = def.Icon,
                    ViewModelType = def.VmType,
                    Permission = def.Permission,
                    Title = L[def.Key]
                });
            }
            if (section.Items.Count > 0)
                MenuSections.Add(section);
        }
    }

    partial void OnNavFilterChanged(string value)
    {
        var filter = value.Trim();
        foreach (var section in MenuSections)
        {
            var visible = 0;
            foreach (var item in section.Items)
            {
                item.IsVisible = filter.Length == 0 || item.Title.Contains(filter, StringComparison.OrdinalIgnoreCase);
                if (item.IsVisible) visible++;
            }
            section.IsVisible = visible > 0;
        }
    }

    private void OnMenuNavigationRequested(string menuKey)
    {
        var item = MenuSections.SelectMany(s => s.Items).FirstOrDefault(m => m.Key == menuKey);
        if (item is not null)
            SelectedMenuItem = item;
    }

    partial void OnSelectedMenuItemChanged(MenuItem? oldValue, MenuItem? newValue)
    {
        if (oldValue is not null) oldValue.IsActive = false;
        if (newValue is null) return;

        newValue.IsActive = true;
        CurrentPage = (ViewModelBase)ServiceLocator.Resolve(newValue.ViewModelType);
        CurrentPageTitle = newValue.Title;
        if (CurrentPage is ILoadable loadable)
            _ = loadable.LoadAsync();
    }

    partial void OnCurrentThemeChanged(AppTheme value)
    {
        SettingsService.Instance.Theme = value;
        if (ThemeManager.Instance.Theme != value)
            ThemeManager.Instance.Theme = value;
        OnPropertyChanged(nameof(IsDarkTheme));
        OnPropertyChanged(nameof(ThemeIcon));
    }

    partial void OnCurrentLanguageChanged(AppLanguage value)
    {
        SettingsService.Instance.Language = value;
        if (LocalizationManager.Instance.CurrentLanguage != value)
            LocalizationManager.Instance.CurrentLanguage = value;
        OnPropertyChanged(nameof(CurrentLanguageFlag));
        RefreshTitles();
    }

    private void OnThemeManagedChanged(AppTheme theme)
    {
        if (CurrentTheme != theme)
            CurrentTheme = theme;
    }

    private void OnLanguageManagedChanged()
    {
        var lang = LocalizationManager.Instance.CurrentLanguage;
        if (CurrentLanguage != lang)
            CurrentLanguage = lang;
    }

    private void RefreshTitles()
    {
        foreach (var section in MenuSections)
        {
            section.Title = L[SectionDefs.First(s => s.Key == section.Key).TitleKey];
            foreach (var item in section.Items)
                item.Title = L[item.Key];
        }
        if (SelectedMenuItem is not null)
            CurrentPageTitle = SelectedMenuItem.Title;
    }

    [RelayCommand]
    private void ToggleThemePopup() { IsThemePopupOpen = !IsThemePopupOpen; IsLanguagePopupOpen = false; IsUserMenuOpen = false; }

    [RelayCommand]
    private void ToggleLanguagePopup() { IsLanguagePopupOpen = !IsLanguagePopupOpen; IsThemePopupOpen = false; IsUserMenuOpen = false; }

    [RelayCommand]
    private void ToggleUserMenu() { IsUserMenuOpen = !IsUserMenuOpen; IsThemePopupOpen = false; IsLanguagePopupOpen = false; }

    [RelayCommand]
    private void SelectTheme(string theme) { CurrentTheme = Enum.Parse<AppTheme>(theme); IsThemePopupOpen = false; }

    [RelayCommand]
    private void SelectLanguage(AppLanguage lang) { CurrentLanguage = lang; IsLanguagePopupOpen = false; }

    [RelayCommand]
    private void ToggleSidebar() => IsSidebarCollapsed = !IsSidebarCollapsed;

    [RelayCommand]
    private void SelectMenuItem(MenuItem item) => SelectedMenuItem = item;

    [RelayCommand]
    private void Logout()
    {
        IsUserMenuOpen = false;
        _authService.Logout();
        _navigationService.NavigateTo(ServiceLocator.Resolve<LoginViewModel>());
    }
}
