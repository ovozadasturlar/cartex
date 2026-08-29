using System.Collections.ObjectModel;
using System.ComponentModel;
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
    private readonly IDialogService _dialogService;
    private readonly Cartex.ApiClient.Api.IBusinessApi _businessApi;
    private readonly Action _langChangedHandler;
    private PeriodicTimer? _bootstrapRetryTimer;
    private PeriodicTimer? _pageRetryTimer;
    private ViewModelBase? _failedPage;
    private int _bootstrapGeneration;
    private int _bootstrapRetryAttempt;
    private int _pageRetryAttempt;

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
    [ObservableProperty] private bool _isPageSwitcherOpen;
    [ObservableProperty] private PageSwitchItem? _selectedPageSwitchItem;
    public ObservableCollection<ShortcutHelpRow> ShortcutHelpRows { get; } = [];
    public ObservableCollection<PageSwitchItem> PageSwitchItems { get; } = [];
    private readonly List<string> _recentPageKeys = [];

    public bool IsShellOverlayOpen => IsPaletteOpen || IsShortcutHelpOpen || IsOnboardingOpen || IsPageSwitcherOpen;
    public bool BlurCurrentPage => IsDialogOpen && CurrentPage is not SettingsHubViewModel;
    partial void OnIsPaletteOpenChanged(bool value) => OnPropertyChanged(nameof(IsShellOverlayOpen));
    partial void OnIsShortcutHelpOpenChanged(bool value) => OnPropertyChanged(nameof(IsShellOverlayOpen));
    partial void OnIsOnboardingOpenChanged(bool value) => OnPropertyChanged(nameof(IsShellOverlayOpen));
    partial void OnIsPageSwitcherOpenChanged(bool value) => OnPropertyChanged(nameof(IsShellOverlayOpen));
    partial void OnIsDialogOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(BlurCurrentPage));
        if (CurrentPage is SettingsHubViewModel settings)
            settings.IsDialogOpen = value;
    }

    [RelayCommand]
    private void ToggleShortcutHelp()
    {
        if (!IsShortcutHelpOpen)
        {
            ShortcutHelpRows.Clear();
            ShortcutHelpRows.Add(new ShortcutHelpRow("Ctrl+K", L["shortcut_palette"]));
            ShortcutHelpRows.Add(new ShortcutHelpRow("Ctrl+Tab", L["shortcut_recent_pages"]));
            ShortcutHelpRows.Add(new ShortcutHelpRow("F1", L["shortcut_help"]));
            foreach (var s in _shortcuts.Current)
                ShortcutHelpRows.Add(new ShortcutHelpRow(s.Gesture, L[s.LabelKey]));
        }
        IsShortcutHelpOpen = !IsShortcutHelpOpen;
    }

    public MainViewModel(AuthService authService, NavigationService navigationService, BranchContextService branch, IBusyService busy, ConnectivityService connectivity, Cartex.ApiClient.Api.IBusinessApi businessApi, Cartex.ApiClient.Api.IFeaturesApi featuresApi, Cartex.ApiClient.Api.ISettingsApi settingsApi, ReferenceCache cache, ShortcutService shortcuts, IDialogService dialogService)
    {
        _shortcuts = shortcuts;
        _authService = authService;
        _navigationService = navigationService;
        _dialogService = dialogService;
        _businessApi = businessApi;
        _featuresApi = featuresApi;
        _settingsApi = settingsApi;
        _cache = cache;
        _authService.LoggedOut += OnSessionEnded;
        Branch = branch;
        Branch.PropertyChanged += OnBranchPropertyChanged;
        Busy = busy;
        Connectivity = connectivity;
        Connectivity.PropertyChanged += OnConnectivityPropertyChanged;
        _currentTheme = SettingsService.Instance.Theme;
        _currentLanguage = SettingsService.Instance.Language;

        _navigationService.MenuNavigationRequested += OnMenuNavigationRequested;
        _navigationService.FeaturesRefreshRequested += LoadFeaturesAsync;
        _navigationService.PageNavigationRequested += OnPageNavigationRequested;
        if (dialogService is DialogService dialogs)
            dialogs.OpenChanged += open => Avalonia.Threading.Dispatcher.UIThread.Post(() => IsDialogOpen = open);
        ThemeManager.Instance.ThemeChanged += OnThemeManagedChanged;
        _langChangedHandler = OnLanguageManagedChanged;
        LocalizationManager.Instance.LanguageChanged += _langChangedHandler;
    }

    private void OnSessionEnded()
    {
        _bootstrapGeneration++;
        CancelBootstrapRetry();
        CancelPageRetry();
        ServiceLocator.TryResolve<Cartex.ApiClient.PageRequestScope>()?.CancelPending();
        CurrentPage?.OnNavigatedFrom();
    }

    private void OnBranchPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BranchContextService.HasMultipleWarehouses))
        {
            RefreshMenuVisibility();
            BuildPalette();
        }
    }

    private async void OnConnectivityPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ConnectivityService.IsOnline) || !Connectivity.IsOnline) return;
        try
        {
            await Branch.LoadAsync();
            if (_failedPage is { } failed)
                await RetryFailedPageAsync(failed);
            else if (CurrentPage is ILoadable)
                await LoadCurrentPageAsync(CurrentPage);
        }
        catch
        {
        }
    }

    private void RefreshMenuVisibility()
    {
        var filter = NavFilter.Trim();
        foreach (var section in MenuSections)
        {
            var visible = 0;
            foreach (var item in section.Items)
            {
                item.IsVisible = (filter.Length == 0 || item.Title.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    && (item.Key != "transfers" || Branch.HasMultipleWarehouses);
                if (item.IsVisible) visible++;
            }
            section.IsVisible = visible > 0;
        }
    }

    public void Initialize()
    {
        var generation = ++_bootstrapGeneration;
        UserDisplayName = _authService.UserInfo?.FullName ?? _authService.UserInfo?.Username ?? "";
        UserRole = string.Join(", ", _authService.Roles);
        OnPropertyChanged(nameof(UserInitial));

        Connectivity.Start();
        ServiceLocator.Resolve<OfflineSyncService>().Start();
        ShowBootstrapLoading();
        _ = InitializeAsync(generation);
    }

    private async Task InitializeAsync(int generation)
    {
        try
        {
            await CompleteBootstrapAsync(
                Task.WhenAll(RefreshFeaturesAsync(), RefreshPolicyAsync()), Branch.LoadAsync(), () =>
            {
                if (generation != _bootstrapGeneration) return;
                BuildMenu();
                CanOpenSettings = NavRegistry.SettingsPages.Any(p =>
                    p.Permission is not null && p.IsAvailable(_authService.HasPermission));
                OnPropertyChanged(nameof(CanOpenSettings));
                BuildPalette();
                SelectLanding();
            });
        }
        catch (Exception exception)
        {
            if (generation != _bootstrapGeneration) return;
            ShowBootstrapFailure(exception, generation);
            return;
        }

        if (generation != _bootstrapGeneration) return;
        CancelBootstrapRetry();
        _bootstrapRetryAttempt = 0;
        var hub = ServiceLocator.Resolve<HubHostService>();
        hub.Start();
        RunBackground(hub.RefreshAttestationAsync);
        RunBackground(ServiceLocator.Resolve<PrintHostService>().StartAsync);
        RunBackground(ServiceLocator.Resolve<PrintStatusHubService>().EnsureStartedAsync);
        RunBackground(ServiceLocator.Resolve<PrintLogoCache>().WarmCurrentAsync);
        RunBackground(CheckOnboardingAsync);
    }

    internal static async Task CompleteBootstrapAsync(Task features, Task branch, Action selectLanding)
    {
        await Task.WhenAll(features, branch);
        selectLanding();
    }

    private void ShowBootstrapLoading()
    {
        CurrentPageTitle = L["loading"];
        CurrentPage = new PageStatusViewModel(L["loading"], string.Empty, true);
    }

    private void ShowBootstrapFailure(Exception exception, int generation)
    {
        CurrentPageTitle = L["bootstrap_failed_title"];
        CurrentPage = new PageStatusViewModel(
            L["bootstrap_failed_title"],
            ApiErrors.Describe(exception),
            false,
            () => RetryBootstrapAsync(generation));
        ScheduleBootstrapRetry(generation);
    }

    private Task RetryBootstrapAsync(int generation)
    {
        if (generation != _bootstrapGeneration) return Task.CompletedTask;
        CancelBootstrapRetry();
        ShowBootstrapLoading();
        return InitializeAsync(generation);
    }

    private void ScheduleBootstrapRetry(int generation)
    {
        CancelBootstrapRetry();
        var delay = TimeSpan.FromSeconds(Math.Min(Math.Pow(2, _bootstrapRetryAttempt++), 30));
        var timer = _bootstrapRetryTimer = new PeriodicTimer(delay);
        _ = RetryBootstrapAfterAsync(timer, generation);
    }

    private async Task RetryBootstrapAfterAsync(
        PeriodicTimer timer,
        int generation)
    {
        try
        {
            if (await timer.WaitForNextTickAsync())
                await RetryBootstrapAsync(generation);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void CancelBootstrapRetry()
    {
        _bootstrapRetryTimer?.Dispose();
        _bootstrapRetryTimer = null;
    }

    private async Task CheckOnboardingAsync()
    {
        if (!_authService.HasPermission("business.edit")) return;
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
            if (item.Key == "transfers" && !Branch.HasMultipleWarehouses)
                continue;
            var captured = item;
            _allPaletteItems.Add(new PaletteItem(item.Title, item.Icon, () => SelectedMenuItem = captured));
        }
        foreach (var def in NavRegistry.SettingsPages)
        {
            if (!def.IsAvailable(_authService.HasPermission)) continue;
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
                if (!def.IsAvailable(_authService.HasPermission))
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
        RefreshMenuVisibility();
    }

    private readonly HashSet<string> _enabledFeatures = new(SettingsService.Instance.EnabledFeatures);
    private Cartex.ApiClient.Api.IFeaturesApi _featuresApi = null!;
    private readonly Cartex.ApiClient.Api.ISettingsApi _settingsApi;
    private readonly ReferenceCache _cache;

    private async Task LoadFeaturesAsync()
    {
        try
        {
            var changed = await RefreshFeaturesAsync();
            changed |= await RefreshPolicyAsync();
            if (!changed) return;
            var openKey = SelectedMenuItem?.Key;
            BuildMenu();
            RestoreSelection(openKey);
            BuildPalette();
        }
        catch { }
    }

    private async Task<bool> RefreshFeaturesAsync()
    {
        var enabled = await _featuresApi.GetEnabledAsync();
        if (_enabledFeatures.SetEquals(enabled)) return false;
        _enabledFeatures.Clear();
        foreach (var code in enabled) _enabledFeatures.Add(code);
        SettingsService.Instance.EnabledFeatures = [.. _enabledFeatures];
        return true;
    }

    /// BRAK-06: menyudagi chiqim bo'limi siyosatga qarab ochiladi. Siyosat yetib kelmasa
    /// oxirgi ma'lum holat qoladi — bo'limni yashirish uchun bootstrap to'xtatilmaydi.
    private async Task<bool> RefreshPolicyAsync()
    {
        try
        {
            var policy = await _cache.GetAsync(CacheKeys.SalesPolicy, _settingsApi.GetSalesPolicyAsync);
            if (SettingsService.Instance.TrackWriteOff == policy.TrackWriteOff) return false;
            SettingsService.Instance.TrackWriteOff = policy.TrackWriteOff;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// Menyu qayta qurilgach ro'yxatdagi obyektlar almashadi va tanlov uziladi; ochiq sahifa
    /// kaliti bo'yicha tiklanadi, tiklab bo'lmasa (imkoniyat o'chirilgan yoki hali hech narsa
    /// ochilmagan) boshlang'ich sahifa tanlanadi — aks holda oyna bo'sh qolardi.
    private void RestoreSelection(string? openKey)
    {
        if (IsSettingsActive) return;
        var restored = openKey is null
            ? null
            : MenuSections.SelectMany(s => s.Items).FirstOrDefault(m => m.Key == openKey);
        if (restored is not null)
            SelectedMenuItem = restored;
        else
            SelectLanding();
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
        RefreshMenuVisibility();
    }

    private void OnMenuNavigationRequested(string menuKey)
    {
        var item = MenuSections.SelectMany(s => s.Items).FirstOrDefault(m => m.Key == menuKey);
        if (item is not null)
            SelectedMenuItem = item;
    }

    private void OnPageNavigationRequested(ViewModelBase page)
    {
        CurrentPage = page;
        StartCurrentPageLoad(page);
    }

    partial void OnCurrentPageChanged(ViewModelBase? oldValue, ViewModelBase? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.OnNavigatedFrom();
            _dialogService.CloseOverlay();
        }
        ServiceLocator.Resolve<Cartex.ApiClient.PageRequestScope>().CancelPending();
        if (oldValue != _failedPage)
            (oldValue as IDisposable)?.Dispose();
        OnPropertyChanged(nameof(BlurCurrentPage));
        if (newValue is SettingsHubViewModel settings)
            settings.IsDialogOpen = IsDialogOpen;
    }

    partial void OnSelectedMenuItemChanged(MenuItem? oldValue, MenuItem? newValue)
    {
        if (oldValue is not null) oldValue.IsActive = false;
        if (newValue is null) return;

        IsSettingsActive = false;
        newValue.IsActive = true;
        // Menyu qayta qurilganda band obyekti almashadi; ayni sahifa ochiq bo'lsa uni qayta
        // yaratish savat kabi to'ldirilgan holatni yo'qotardi.
        if (CanReusePage(oldValue?.Key, newValue.Key, newValue.ViewModelType, CurrentPage))
            return;
        RememberPage(newValue.Key);
        CurrentPage = (ViewModelBase)ServiceLocator.Resolve(newValue.ViewModelType);
        CurrentPageTitle = newValue.Title;
        StartCurrentPageLoad(CurrentPage);
    }

    [RelayCommand]
    private void OpenSettings()
    {
        if (!CanOpenSettings) return;
        SelectedMenuItem = null;
        IsSettingsActive = true;
        RememberPage("__settings");
        CurrentPage = ServiceLocator.Resolve<SettingsHubViewModel>();
        CurrentPageTitle = L["settings"];
        StartCurrentPageLoad(CurrentPage);
    }

    internal static bool CanReusePage(string? oldKey, string newKey, Type pageType, object? page) =>
        oldKey == newKey
        && page?.GetType() == pageType
        && page is ILoadable { LoadState: PageLoadState.Loaded };

    internal static async Task LoadPageAsync(object? page)
    {
        if (page is not ILoadable loadable) return;
        if (page is ViewModelBase viewModel)
            viewModel.LoadState = PageLoadState.Loading;
        try
        {
            var scope = ServiceLocator.TryResolve<Cartex.ApiClient.PageRequestScope>();
            if (scope is null)
                await loadable.LoadAsync();
            else
                using (scope.BeginPageRequest())
                    await loadable.LoadAsync();
            if (page is ViewModelBase loaded)
                loaded.LoadState = PageLoadState.Loaded;
        }
        catch (OperationCanceledException)
        {
            if (page is ViewModelBase cancelled)
                cancelled.LoadState = PageLoadState.NotLoaded;
            throw;
        }
        catch
        {
            if (page is ViewModelBase failed)
                failed.LoadState = PageLoadState.Failed;
            throw;
        }
    }

    private void StartCurrentPageLoad(ViewModelBase? page) => _ = LoadCurrentPageAsync(page);

    private async Task LoadCurrentPageAsync(ViewModelBase? page)
    {
        CancelPageRetry();
        if (_failedPage is IDisposable disposable && _failedPage != page)
            disposable.Dispose();
        _failedPage = null;
        try
        {
            await LoadPageAsync(page);
            _pageRetryAttempt = 0;
        }
        catch (Exception exception) when (ApiErrors.IsCancelled(exception))
        {
        }
        catch (Exception exception)
        {
            if (page is null || CurrentPage != page) return;
            _failedPage = page;
            var status = new PageStatusViewModel(
                L["page_load_failed_title"],
                ApiErrors.Describe(exception),
                false,
                () => RetryFailedPageAsync(page));
            CurrentPage = status;
            SchedulePageRetry(page, status);
        }
    }

    private async Task RetryFailedPageAsync(ViewModelBase page)
    {
        CancelPageRetry();
        CurrentPage = page;
        await LoadCurrentPageAsync(page);
    }

    private void SchedulePageRetry(ViewModelBase page, PageStatusViewModel status)
    {
        CancelPageRetry();
        var delay = TimeSpan.FromSeconds(Math.Min(Math.Pow(2, _pageRetryAttempt++), 30));
        var timer = _pageRetryTimer = new PeriodicTimer(delay);
        _ = RetryPageAfterAsync(timer, page, status);
    }

    private async Task RetryPageAfterAsync(
        PeriodicTimer timer,
        ViewModelBase page,
        PageStatusViewModel status)
    {
        try
        {
            if (await timer.WaitForNextTickAsync() && CurrentPage == status)
                await RetryFailedPageAsync(page);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void CancelPageRetry()
    {
        _pageRetryTimer?.Dispose();
        _pageRetryTimer = null;
    }

    private static void RunBackground(Func<Task> work) => _ = RunBackgroundAsync(work);

    private static async Task RunBackgroundAsync(Func<Task> work)
    {
        try
        {
            await work();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(exception);
        }
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

    public void BeginPageSwitch(int direction)
    {
        RebuildPageSwitchItems();
        if (PageSwitchItems.Count < 2)
            return;

        IsPageSwitcherOpen = true;
        SelectPageSwitchItem(direction > 0 ? 1 : PageSwitchItems.Count - 1);
    }

    public void CyclePageSwitch(int direction)
    {
        if (!IsPageSwitcherOpen)
        {
            BeginPageSwitch(direction);
            return;
        }
        if (PageSwitchItems.Count == 0)
            return;

        var current = SelectedPageSwitchItem is null ? 0 : PageSwitchItems.IndexOf(SelectedPageSwitchItem);
        SelectPageSwitchItem((current + direction + PageSwitchItems.Count) % PageSwitchItems.Count);
    }

    public void CommitPageSwitch()
    {
        var target = SelectedPageSwitchItem;
        ClosePageSwitch();
        if (target is null)
            return;
        if (target.Key == "__settings")
        {
            OpenSettingsCommand.Execute(null);
            return;
        }

        var item = MenuSections.SelectMany(section => section.Items).FirstOrDefault(candidate => candidate.Key == target.Key);
        if (item is not null)
            SelectedMenuItem = item;
    }

    public void CancelPageSwitch() => ClosePageSwitch();

    private void RebuildPageSwitchItems()
    {
        PageSwitchItems.Clear();
        foreach (var key in _recentPageKeys)
        {
            if (key == "__settings")
            {
                if (CanOpenSettings)
                    PageSwitchItems.Add(new PageSwitchItem(key, L["settings"], Material.Icons.MaterialIconKind.Cog));
                continue;
            }

            var item = MenuSections.SelectMany(section => section.Items).FirstOrDefault(candidate => candidate.Key == key);
            if (item is not null)
                PageSwitchItems.Add(new PageSwitchItem(key, item.Title, item.Icon));
        }
    }

    private void SelectPageSwitchItem(int index)
    {
        foreach (var item in PageSwitchItems)
            item.IsSelected = false;
        SelectedPageSwitchItem = PageSwitchItems[index];
        SelectedPageSwitchItem.IsSelected = true;
    }

    private void RememberPage(string key)
    {
        _recentPageKeys.Remove(key);
        _recentPageKeys.Insert(0, key);
        if (_recentPageKeys.Count > 8)
            _recentPageKeys.RemoveRange(8, _recentPageKeys.Count - 8);
    }

    private void ClosePageSwitch()
    {
        IsPageSwitcherOpen = false;
        SelectedPageSwitchItem = null;
        PageSwitchItems.Clear();
    }

    [RelayCommand]
    private void Logout()
    {
        IsUserMenuOpen = false;
        _authService.Logout();
        _navigationService.NavigateTo(ServiceLocator.Resolve<LoginViewModel>());
    }
}

public sealed record ShortcutHelpRow(string Gesture, string Label);

public sealed partial class PageSwitchItem(string key, string title, Material.Icons.MaterialIconKind icon) : ObservableObject
{
    public string Key { get; } = key;
    public string Title { get; } = title;
    public Material.Icons.MaterialIconKind Icon { get; } = icon;
    [ObservableProperty] private bool _isSelected;
}
