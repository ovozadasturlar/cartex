using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.UI.Models;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class SettingsHubViewModel : ViewModelBase, ILoadable
{
    private readonly AuthService _authService;
    private readonly IDialogService _dialogService;
    private PeriodicTimer? _retryTimer;
    private int _retryAttempt;

    [ObservableProperty] private ViewModelBase? _currentSection;
    [ObservableProperty] private MenuItem? _selectedSection;
    [ObservableProperty] private bool _isSidebarCollapsed = SettingsService.Instance.SettingsSidebarCollapsed;
    [ObservableProperty] private bool _isDialogOpen;

    public double SidebarWidth => IsSidebarCollapsed ? 64 : 248;

    partial void OnIsSidebarCollapsedChanged(bool value)
    {
        SettingsService.Instance.SettingsSidebarCollapsed = value;
        OnPropertyChanged(nameof(SidebarWidth));
    }

    [RelayCommand]
    private void ToggleSidebar() => IsSidebarCollapsed = !IsSidebarCollapsed;

    public ObservableCollection<MenuSection> Sections { get; } = [];

    public SettingsHubViewModel(AuthService authService, IDialogService dialogService)
    {
        _authService = authService;
        _dialogService = dialogService;
        LocalizationManager.Instance.LanguageChanged += RefreshTitles;
    }

    public Task LoadAsync()
    {
        Build();
        SelectedSection = Sections.SelectMany(s => s.Items).FirstOrDefault();
        return Task.CompletedTask;
    }

    private void Build()
    {
        Sections.Clear();
        foreach (var (key, titleKey) in NavRegistry.SettingsSections)
        {
            var section = new MenuSection { Key = key, Title = L[titleKey] };
            foreach (var def in NavRegistry.SettingsPages.Where(d => d.SectionKey == key))
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
                Sections.Add(section);
        }
    }

    partial void OnSelectedSectionChanged(MenuItem? oldValue, MenuItem? newValue)
    {
        if (oldValue is not null) oldValue.IsActive = false;
        if (newValue is null) return;
        if (oldValue is not null)
        {
            CurrentSection?.OnNavigatedFrom();
            _dialogService.CloseOverlay();
        }
        newValue.IsActive = true;
        ServiceLocator.Resolve<Cartex.ApiClient.PageRequestScope>().CancelPending();
        CurrentSection = (ViewModelBase)ServiceLocator.Resolve(newValue.ViewModelType);
        _ = LoadSectionAsync(CurrentSection);
    }

    private async Task LoadSectionAsync(ViewModelBase section)
    {
        CancelRetry();
        try
        {
            await MainViewModel.LoadPageAsync(section);
            _retryAttempt = 0;
        }
        catch (Exception exception) when (ApiErrors.IsCancelled(exception))
        {
        }
        catch (Exception exception)
        {
            if (CurrentSection != section) return;
            var status = new PageStatusViewModel(
                L["page_load_failed_title"],
                ApiErrors.Describe(exception),
                false,
                () => RetrySectionAsync(section));
            CurrentSection = status;
            ScheduleRetry(section, status);
        }
    }

    private async Task RetrySectionAsync(ViewModelBase section)
    {
        CancelRetry();
        CurrentSection = section;
        await LoadSectionAsync(section);
    }

    private void ScheduleRetry(ViewModelBase section, PageStatusViewModel status)
    {
        CancelRetry();
        var delay = TimeSpan.FromSeconds(Math.Min(Math.Pow(2, _retryAttempt++), 30));
        var timer = _retryTimer = new PeriodicTimer(delay);
        _ = RetryAfterAsync(timer, section, status);
    }

    private async Task RetryAfterAsync(
        PeriodicTimer timer,
        ViewModelBase section,
        PageStatusViewModel status)
    {
        try
        {
            if (await timer.WaitForNextTickAsync() && CurrentSection == status)
                await RetrySectionAsync(section);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void CancelRetry()
    {
        _retryTimer?.Dispose();
        _retryTimer = null;
    }

    private void RefreshTitles()
    {
        foreach (var section in Sections)
        {
            section.Title = L[NavRegistry.SettingsSections.First(s => s.Key == section.Key).TitleKey];
            foreach (var item in section.Items)
                item.Title = L[item.Key];
        }
    }

    [RelayCommand]
    private void SelectSection(MenuItem item) => SelectedSection = item;

    public void SelectByKey(string key)
    {
        var item = Sections.SelectMany(s => s.Items).FirstOrDefault(i => i.Key == key);
        if (item is not null) SelectedSection = item;
    }

    public void SelectNotificationJournal()
    {
        SelectByKey("notification_journal");
        if (CurrentSection is NotificationJournalViewModel journal)
            journal.SelectSmsChannel();
    }

    public override void OnNavigatedFrom()
    {
        CancelRetry();
        CurrentSection?.OnNavigatedFrom();
        _dialogService.CloseOverlay();
    }
}
