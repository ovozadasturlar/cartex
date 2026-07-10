using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.UI.Models;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly AuthService _authService;
    private readonly ShortcutService _shortcuts;
    private readonly NavigationService _navigationService;
    private readonly Cartex.ApiClient.Api.IBusinessApi _businessApi;
    private readonly Action _langChangedHandler;

    public BranchContextService Branch { get; }
    public IBusyService Busy { get; }
    public ConnectivityService Connectivity { get; }

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
    [ObservableProperty] private bool _isSettingsActive;

    public bool CanOpenSettings { get; private set; }

    public string UserInitial => string.IsNullOrEmpty(UserDisplayName) ? "?" : UserDisplayName[..1].ToUpper();
    public bool IsDarkTheme { get => CurrentTheme == AppTheme.Dark; set => CurrentTheme = value ? AppTheme.Dark : AppTheme.Light; }
    public string CurrentLanguageFlag => LocalizationManager.GetLanguageShortCode(CurrentLanguage);
    public Material.Icons.MaterialIconKind ThemeIcon => CurrentTheme == AppTheme.Dark ? Material.Icons.MaterialIconKind.WeatherNight : Material.Icons.MaterialIconKind.WeatherSunny;

    public ObservableCollection<MenuSection> MenuSections { get; } = [];
    public AppLanguage[] AvailableLanguages => LocalizationManager.AvailableLanguages;

    [ObservableProperty] private bool _isOnboardingOpen;
    [ObservableProperty] private OnboardingViewModel? _onboarding;

    private readonly List<PaletteItem> _allPaletteItems = [];
    public ObservableCollection<PaletteItem> PaletteResults { get; } = [];
    [ObservableProperty] private bool _isPaletteOpen;
    [ObservableProperty] private string _paletteQuery = "";
    [ObservableProperty] private PaletteItem? _selectedPaletteItem;

    [ObservableProperty] private bool _isShortcutHelpOpen;
    [ObservableProperty] private bool _isDialogOpen;
    public ObservableCollection<ShortcutHelpRow> ShortcutHelpRows { get; } = [];

    public bool IsShellOverlayOpen => IsPaletteOpen || IsShortcutHelpOpen || IsOnboardingOpen || IsDialogOpen;
    partial void OnIsPaletteOpenChanged(bool value) => OnPropertyChanged(nameof(IsShellOverlayOpen));
    partial void OnIsShortcutHelpOpenChanged(bool value) => OnPropertyChanged(nameof(IsShellOverlayOpen));
    partial void OnIsOnboardingOpenChanged(bool value) => OnPropertyChanged(nameof(IsShellOverlayOpen));
    partial void OnIsDialogOpenChanged(bool value) => OnPropertyChanged(nameof(IsShellOverlayOpen));

    [RelayCommand]
    private void ToggleShortcutHelp()
    {
        if (!IsShortcutHelpOpen)
        {
            ShortcutHelpRows.Clear();
            ShortcutHelpRows.Add(new ShortcutHelpRow("Ctrl+K", L["shortcut_palette"]));
            ShortcutHelpRows.Add(new ShortcutHelpRow("F1", L["shortcut_help"]));
            foreach (var s in _shortcuts.Current)
                ShortcutHelpRows.Add(new ShortcutHelpRow(s.Gesture, L[s.LabelKey]));
        }
        IsShortcutHelpOpen = !IsShortcutHelpOpen;
    }

    public MainViewModel(AuthService authService, NavigationService navigationService, BranchContextService branch, IBusyService busy, ConnectivityService connectivity, Cartex.ApiClient.Api.IBusinessApi businessApi, Cartex.ApiClient.Api.IFeaturesApi featuresApi, ShortcutService shortcuts)
    {
        _shortcuts = shortcuts;
        _authService = authService;
        _navigationService = navigationService;
        _businessApi = businessApi;
        _featuresApi = featuresApi;
        Branch = branch;
        Busy = busy;
        Connectivity = connectivity;
        _currentTheme = SettingsService.Instance.Theme;
        _currentLanguage = SettingsService.Instance.Language;

        _navigationService.MenuNavigationRequested += OnMenuNavigationRequested;
        if (ServiceLocator.Resolve<IDialogService>() is DialogService dialogs)
            dialogs.OpenChanged += open => Avalonia.Threading.Dispatcher.UIThread.Post(() => IsDialogOpen = open);
        ThemeManager.Instance.ThemeChanged += OnThemeManagedChanged;
        _langChangedHandler = OnLanguageManagedChanged;
        LocalizationManager.Instance.LanguageChanged += _langChangedHandler;
    }

    public void Initialize()
    {
        UserDisplayName = _authService.UserInfo?.FullName ?? _authService.UserInfo?.Username ?? "";
        UserRole = string.Join(", ", _authService.Roles);
        OnPropertyChanged(nameof(UserInitial));

        BuildMenu();
        CanOpenSettings = NavRegistry.SettingsPages.Any(p => p.Permission is not null && _authService.HasPermission(p.Permission));
        OnPropertyChanged(nameof(CanOpenSettings));
        BuildPalette();
        _ = LoadFeaturesAsync();
        Connectivity.Start();
        ServiceLocator.Resolve<OfflineSyncService>().Start();
        _ = Branch.LoadAsync();
        _ = CheckOnboardingAsync();

        SelectLanding();
    }

    private async Task CheckOnboardingAsync()
    {
        if (!_authService.HasPermission("business.manage")) return;
        try
        {
            var business = await _businessApi.GetAsync();
            if (business.IsOnboarded) return;
            var vm = ServiceLocator.Resolve<OnboardingViewModel>();
            await vm.LoadAsync();
            vm.Completed = () => IsOnboardingOpen = false;
            Onboarding = vm;
            IsOnboardingOpen = true;
        }
        catch { }
    }

    private void BuildPalette()
    {
        _allPaletteItems.Clear();
        foreach (var item in MenuSections.SelectMany(s => s.Items))
        {
            var captured = item;
            _allPaletteItems.Add(new PaletteItem(item.Title, item.Icon, () => SelectedMenuItem = captured));
        }
        foreach (var def in NavRegistry.SettingsPages)
        {
            if (def.Permission is not null && !_authService.HasPermission(def.Permission)) continue;
            var key = def.Key;
            _allPaletteItems.Add(new PaletteItem(L[key], def.Icon, () => OpenSettingsPage(key)));
        }
    }

    private void OpenSettingsPage(string key)
    {
        OpenSettings();
        ServiceLocator.Resolve<SettingsHubViewModel>().SelectByKey(key);
    }

    private void FilterPalette()
    {
        var q = PaletteQuery.Trim();
        PaletteResults.Clear();
        foreach (var item in _allPaletteItems)
            if (q.Length == 0 || item.Title.Contains(q, StringComparison.OrdinalIgnoreCase))
                PaletteResults.Add(item);
        SelectedPaletteItem = PaletteResults.FirstOrDefault();
    }

    partial void OnPaletteQueryChanged(string value) => FilterPalette();

    [RelayCommand]
    private void OpenPalette()
    {
        PaletteQuery = "";
        FilterPalette();
        IsPaletteOpen = true;
    }

    [RelayCommand]
    private void ClosePalette() => IsPaletteOpen = false;

    [RelayCommand]
    private void ExecutePalette(PaletteItem? item)
    {
        item ??= SelectedPaletteItem;
        if (item is null) return;
        IsPaletteOpen = false;
        item.Invoke();
    }

    private void BuildMenu()
    {
        MenuSections.Clear();
        foreach (var (key, titleKey) in NavRegistry.SidebarSections)
        {
            var section = new MenuSection { Key = key, Title = L[titleKey] };
            foreach (var def in NavRegistry.SidebarPages.Where(d => d.SectionKey == key))
            {
                if (def.Permission is not null && !_authService.HasPermission(def.Permission))
                    continue;
                if (def.Feature is not null && !_enabledFeatures.Contains(def.Feature))
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

    private readonly HashSet<string> _enabledFeatures = [];
    private Cartex.ApiClient.Api.IFeaturesApi _featuresApi = null!;

    private async Task LoadFeaturesAsync()
    {
        try
        {
            var enabled = await _featuresApi.GetEnabledAsync();
            _enabledFeatures.Clear();
            foreach (var code in enabled) _enabledFeatures.Add(code);
            if (NavRegistry.SidebarPages.Any(p => p.Feature is not null && _enabledFeatures.Contains(p.Feature)))
            {
                BuildMenu();
                BuildPalette();
            }
        }
        catch { }
    }

    private void SelectLanding()
    {
        var all = MenuSections.SelectMany(s => s.Items).ToList();
        var startKey = _authService.UserInfo?.StartPage;
        var landing = (startKey is not null ? all.FirstOrDefault(m => m.Key == startKey) : null) ?? all.FirstOrDefault();
        if (landing is not null)
            SelectedMenuItem = landing;
        else if (CanOpenSettings)
            OpenSettings();
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

    partial void OnCurrentPageChanged(ViewModelBase? oldValue, ViewModelBase? newValue) =>
        (oldValue as IDisposable)?.Dispose();

    partial void OnSelectedMenuItemChanged(MenuItem? oldValue, MenuItem? newValue)
    {
        if (oldValue is not null) oldValue.IsActive = false;
        if (newValue is null) return;

        IsSettingsActive = false;
        newValue.IsActive = true;
        CurrentPage = (ViewModelBase)ServiceLocator.Resolve(newValue.ViewModelType);
        CurrentPageTitle = newValue.Title;
        if (CurrentPage is ILoadable loadable)
            _ = loadable.LoadAsync();
    }

    [RelayCommand]
    private void OpenSettings()
    {
        if (!CanOpenSettings) return;
        SelectedMenuItem = null;
        IsSettingsActive = true;
        CurrentPage = ServiceLocator.Resolve<SettingsHubViewModel>();
        CurrentPageTitle = L["settings"];
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
            section.Title = L[NavRegistry.SidebarSections.First(s => s.Key == section.Key).TitleKey];
            foreach (var item in section.Items)
                item.Title = L[item.Key];
        }
        if (IsSettingsActive)
            CurrentPageTitle = L["settings"];
        else if (SelectedMenuItem is not null)
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

public sealed record ShortcutHelpRow(string Gesture, string Label);
