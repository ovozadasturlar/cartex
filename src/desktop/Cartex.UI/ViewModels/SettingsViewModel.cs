using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.OfflineCache;
using Cartex.Shared.Models.Warehouses;
using Cartex.UI.Models;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class SettingsViewModel(IOfflineCacheApi offlineApi, IWarehousesApi warehousesApi, OfflineSyncService offlineSync, IToastService toast)
    : ViewModelBase, ILoadable
{
    [ObservableProperty] private string _apiUrl = SettingsService.Instance.ApiBaseUrl;
    [ObservableProperty] private AppLanguage _selectedLanguage = SettingsService.Instance.Language;

    [ObservableProperty] private bool _offlineVisible;
    [ObservableProperty] private bool _offlineEnabled = SettingsService.Instance.OfflineCacheEnabled;
    [ObservableProperty] private string? _offlineHolderText;
    [ObservableProperty] private string? _offlineSyncText;
    [ObservableProperty] private WarehouseDto? _selectedOfflineWarehouse;

    public ObservableCollection<WarehouseDto> OfflineWarehouses { get; } = [];

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

    partial void OnSelectedLanguageChanged(AppLanguage value)
    {
        SettingsService.Instance.Language = value;
        LocalizationManager.Instance.CurrentLanguage = value;
    }

    [RelayCommand]
    private void SaveApiUrl() => SettingsService.Instance.ApiBaseUrl = ApiUrl;

    public async Task LoadAsync()
    {
        try
        {
            var state = await offlineApi.GetStateAsync();
            var warehouses = await warehousesApi.GetAllAsync();
            OfflineWarehouses.Clear();
            foreach (var w in warehouses) OfflineWarehouses.Add(w);
            SelectedOfflineWarehouse = OfflineWarehouses.FirstOrDefault(w => w.Id == SettingsService.Instance.OfflineWarehouseId)
                ?? OfflineWarehouses.FirstOrDefault();
            OfflineVisible = true;
            ApplyState(state);
            await RefreshSyncTextAsync();
        }
        catch
        {
            OfflineVisible = false;
        }
    }

    private void ApplyState(OfflineCacheStateDto state)
    {
        OfflineHolderText = state.DeviceId is null
            ? L["offline_cache_free"]
            : state.DeviceId == SettingsService.Instance.DeviceId
                ? L["offline_cache_this"]
                : $"{L["offline_cache_holder"]}: {state.DeviceName}";
    }

    private async Task RefreshSyncTextAsync()
    {
        if (!OfflineEnabled)
        {
            OfflineSyncText = null;
            return;
        }
        var last = await offlineSync.LastSyncTextAsync() ?? "—";
        var pending = await offlineSync.PendingCountAsync();
        var errors = await offlineSync.ErrorCountAsync();
        OfflineSyncText = $"{L["offline_cache_synced"]}: {last} • {L["offline_cache_queue"]}: {pending}" +
                          (errors > 0 ? $" • {L["offline_cache_errors"]}: {errors}" : "");
    }

    [RelayCommand]
    private async Task ToggleOfflineAsync()
    {
        try
        {
            if (!OfflineEnabled)
            {
                if (SelectedOfflineWarehouse is null)
                {
                    toast.Warning(L["offline_cache_warehouse"]);
                    return;
                }
                await offlineApi.ClaimAsync(new ClaimOfflineCacheRequest(SettingsService.Instance.DeviceId, Environment.MachineName));
                SettingsService.Instance.OfflineWarehouseId = SelectedOfflineWarehouse.Id;
                SettingsService.Instance.OfflineCacheEnabled = true;
                OfflineEnabled = true;
                toast.Success(L["success"]);
                await offlineSync.SyncAsync();
            }
            else
            {
                await offlineApi.ReleaseAsync();
                SettingsService.Instance.OfflineCacheEnabled = false;
                OfflineEnabled = false;
                toast.Success(L["success"]);
            }
            await LoadAsync();
        }
        catch (Exception ex)
        {
            toast.Error(ApiErrors.Describe(ex));
        }
    }
}
