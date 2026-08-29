using System.Collections.ObjectModel;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.OfflineCache;
using Cartex.Shared.Models.Warehouses;
using Cartex.UI.Models;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public sealed class OfflineQueueRow(
    OfflineOutboxItem item, string kindText, string? details, bool canCancel, string? warning)
{
    public OfflineOutboxItem Item { get; } = item;
    public string KindText { get; } = kindText;
    public string When { get; } = item.CreatedAt.ToLocalTime().ToString("dd.MM HH:mm");
    public string? Details { get; } = details;
    public string? Error { get; } = item.Error;
    public string? Warning { get; } = warning;
    public bool IsError { get; } = item.Status == "error";

    /// Vakolatdan tashqarida qolgan qator na yuboriladi, na o'tkazib yuboriladi — uning
    /// tugmalari faqat chalg'itardi.
    public bool CanAct { get; } = warning is null && item.Status == "error";
    public bool CanCancel { get; } = canCancel && item.Status == "pending" && item.PushedAt is null;
}

public partial class SettingsViewModel(IOfflineCacheApi offlineApi, IWarehousesApi warehousesApi, OfflineSyncService offlineSync, HubHostService hubHost, IToastService toast, IDialogService dialog, IFilePickerService filePicker)
    : ViewModelBase, ILoadable
{
    [ObservableProperty] private string _apiUrl = SettingsService.Instance.ApiBaseUrl;
    [ObservableProperty] private AppLanguage _selectedLanguage = SettingsService.Instance.Language;

    [ObservableProperty] private bool _offlineVisible;
    [ObservableProperty] private bool _offlineEnabled = SettingsService.Instance.OfflineCacheEnabled;
    [ObservableProperty] private string? _offlineHolderText;
    [ObservableProperty] private string? _offlineSyncText;
    [ObservableProperty] private WarehouseDto? _selectedOfflineWarehouse;
    [ObservableProperty] private bool _offlineAssignedElsewhere;
    [ObservableProperty] private bool _hasQueueRows;
    [ObservableProperty] private Bitmap? _serverQr;
    [ObservableProperty] private string? _serverQrUrl;
    [ObservableProperty] private Bitmap? _hubQr;
    [ObservableProperty] private string? _hubQrUrl;
    private OfflineCacheStateDto? _offlineState;
    private bool _stateHooked;

    public bool CanEnableOffline => !OfflineEnabled && !OfflineAssignedElsewhere;

    /// HUB-02: yoqilgan bo'lsa ham xizmat faqat bulut yopilganda ochiladi.
    public bool HubEnabled
    {
        get => SettingsService.Instance.HubEnabled;
        set
        {
            if (SettingsService.Instance.HubEnabled == value) return;
            SettingsService.Instance.HubEnabled = value;
            OnPropertyChanged();
            _ = ApplyHubAsync();
        }
    }

    [ObservableProperty] private string? _hubStatusText;

    private async Task ApplyHubAsync()
    {
        await hubHost.ApplyAsync();
        RefreshHubStatus();
    }

    private void RefreshHubStatus()
    {
        HubStatusText = !HubEnabled
            ? L["hub_enable_hint"]
            : hubHost.Error
                ? L["hub_port_busy"]
                : hubHost.IsServing && hubHost.Endpoint is { } endpoint
                    ? $"{L["hub_serving"]}: {endpoint}"
                    : L["hub_standby"];
        BuildHubQr();
    }

    /// HUB-11: QR faqat vakolat egasida ko'rsatiladi — u boshqa qurilmani aynan shu
    /// qurilmaga yo'naltiradi, yo'ldoshning manzili esa hech kimga kerak emas.
    private void BuildHubQr()
    {
        var endpoint = HubEnabled && offlineSync.IsEnabled ? hubHost.LinkEndpoint : null;
        HubQrUrl = endpoint?.AbsoluteUri;
        HubQr = endpoint is null ? null : QrService.Generate(Cartex.Hub.HubQr.Format(endpoint));
    }

    public ObservableCollection<WarehouseDto> OfflineWarehouses { get; } = [];
    public ObservableCollection<OfflineQueueRow> QueueRows { get; } = [];

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
    private void SaveApiUrl()
    {
        SettingsService.Instance.ApiBaseUrl = ApiUrl;
        BuildServerQr();
    }

    public async Task LoadAsync()
    {
        BuildServerQr();
        if (!_stateHooked)
        {
            _stateHooked = true;
            offlineSync.StateChanged += () => Dispatcher.UIThread.Post(() => _ = RefreshOfflineStateAsync());
        }
        // Oflaynda server so'rovi javobsiz osilib qolishi mumkin — lokal holat darhol ko'rsatiladi.
        if (!ServiceLocator.Resolve<ConnectivityService>().IsOnline)
        {
            ApplyLocalOfflineState();
            await RefreshOfflineStateAsync();
            return;
        }
        try
        {
            var state = await offlineApi.GetStateAsync();
            var warehouses = await warehousesApi.GetAllAsync();
            OfflineWarehouses.Clear();
            foreach (var w in warehouses) OfflineWarehouses.Add(w);
            SelectedOfflineWarehouse = OfflineWarehouses.FirstOrDefault(w => w.Id == SettingsService.Instance.OfflineWarehouseId)
                ?? OfflineWarehouses.FirstOrDefault();
            OfflineVisible = true;
            _offlineState = state;
            if (SettingsService.Instance.OfflineCacheEnabled
                && (!state.IsCurrentDevice || state.LeaseId != offlineSync.Credential?.LeaseId))
                await offlineSync.DeactivateLocalAsync();
            OfflineEnabled = offlineSync.IsEnabled && state.IsCurrentDevice;
            ApplyState(state);
            await RefreshOfflineStateAsync();
        }
        catch
        {
            ApplyLocalOfflineState();
            await RefreshOfflineStateAsync();
        }
    }

    private void ApplyLocalOfflineState()
    {
        // HUB-03: yo'ldosh rejimida ham navbat va holat ko'rinishi kerak — savdo shu yerda turadi.
        OfflineVisible = offlineSync.IsEnabled || offlineSync.IsSatellite;
        OfflineEnabled = offlineSync.IsEnabled || offlineSync.IsSatellite;
        if (OfflineVisible)
            OfflineHolderText = offlineSync.IsSatellite ? L["hub_satellite"] : L["offline_cache_this"];
    }

    private async Task RefreshOfflineStateAsync()
    {
        await RefreshSyncTextAsync();
        await RefreshQueueAsync();
    }

    /// Navbat oflayn paneli yopiq bo'lganda ham o'qiladi: vakolat ko'chgandan keyin qolgan
    /// qatorlar aynan shu holatda ko'rinmay qolardi.
    private async Task RefreshQueueAsync()
    {
        var canVoid = ServiceLocator.Resolve<AuthService>().HasPermission("sales.void");
        var (leaseId, epoch) = offlineSync.ActiveLease;
        var rows = await offlineSync.GetQueueRowsAsync();
        QueueRows.Clear();
        foreach (var row in rows)
        {
            var orphan = row.LeaseId != leaseId || row.Epoch != epoch;
            QueueRows.Add(new OfflineQueueRow(row, KindLabel(row.Kind), PayloadSummary(row),
                canVoid && !orphan, orphan ? L["offline_queue_orphan"] : null));
        }
        HasQueueRows = QueueRows.Count > 0;
        if (HasQueueRows) OfflineVisible = true;
    }

    private string KindLabel(string kind) => kind switch
    {
        "customer.payment.create" => L["offline_kind_payment"],
        "supply.create" => L["offline_kind_supply"],
        _ => L["offline_kind_sale"]
    };

    private static string? PayloadSummary(OfflineOutboxItem item)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(item.PayloadJson);
            return OfflineEventFormat.Summary(item.Kind, document.RootElement);
        }
        catch
        {
            return null;
        }
    }

    public bool OfflineAllowSales
    {
        get => SettingsService.Instance.OfflineAllowSales;
        set { SettingsService.Instance.OfflineAllowSales = value; OnPropertyChanged(); _ = offlineSync.RepullSnapshotAsync(); }
    }

    public bool OfflineAllowPayments
    {
        get => SettingsService.Instance.OfflineAllowPayments;
        set { SettingsService.Instance.OfflineAllowPayments = value; OnPropertyChanged(); _ = offlineSync.RepullSnapshotAsync(); }
    }

    public bool OfflineAllowSupplies
    {
        get => SettingsService.Instance.OfflineAllowSupplies;
        set { SettingsService.Instance.OfflineAllowSupplies = value; OnPropertyChanged(); _ = offlineSync.RepullSnapshotAsync(); }
    }

    [RelayCommand]
    private async Task ExportQueueAsync()
    {
        try
        {
            var stream = await filePicker.SaveFileAsync($"cartex-navbat-{DateTime.Now:yyyyMMdd-HHmm}", "json");
            if (stream is null) return;
            int count;
            await using (stream)
                count = await offlineSync.ExportQueueAsync(stream);
            toast.Success($"{L["offline_export_done"]}: {count}");
        }
        catch (Exception ex)
        {
            toast.Error(ApiErrors.Describe(ex));
        }
    }

    [RelayCommand]
    private async Task ImportQueueAsync()
    {
        try
        {
            var picked = await filePicker.PickJsonAsync();
            if (picked is null) return;
            OfflineExportFile file;
            await using (picked.Content)
                file = await offlineSync.ParseExportFileAsync(picked.Content);
            var rows = file.Events.Select(x => new OfflineImportRow(
                x, L[OfflineEventFormat.KindKey(x.Kind)], OfflineEventFormat.Summary(x.Kind, x.Payload))).ToList();
            var choice = await dialog.ShowAsync<Views.OfflineImportDialog, OfflineImportDialogViewModel, OfflineImportChoice>(
                new OfflineImportDialogViewModel(rows));
            if (choice is null) return;
            var results = await offlineSync.ImportFileAsync(file,
                choice.SkipEventIds.Count > 0 ? choice.SkipEventIds : null, choice.SkipRejected);
            var rejected = results.Where(x => x.Status == "Rejected").ToList();
            var deferred = results.Count(x => x.Status == "Deferred");
            var skipped = results.Count(x => x.Status == "Skipped");
            var text = $"{L["offline_import_applied"]}: {results.Count(x => x.Status == "Applied")}\n" +
                       $"{L["offline_import_already"]}: {results.Count(x => x.Status == "AlreadyApplied")}" +
                       (skipped > 0 ? $"\n{L["offline_discard"]}: {skipped}" : "") +
                       (deferred > 0 ? $"\n{L["offline_import_deferred"]}: {deferred}" : "");
            if (rejected.Count > 0)
                text += $"\n{L["offline_cache_errors"]}: {rejected.Count}\n" +
                        string.Join("\n", rejected.Take(5).Select(x => $"• №{x.Sequence} — {x.Error}"));
            await dialog.AlertAsync(text, L["offline_import"]);
        }
        catch (Exception ex)
        {
            toast.Error(ApiErrors.Describe(ex));
        }
    }

    [RelayCommand]
    private Task RetryQueueRow(OfflineQueueRow row) => RetryQueueRowAsync(row.Item);

    [RelayCommand]
    private Task DiscardQueueRow(OfflineQueueRow row) => DiscardQueueRowAsync(row.Item);

    [RelayCommand]
    private Task CancelQueueRow(OfflineQueueRow row) => CancelQueueRowAsync(row.Item);

    internal async Task RetryQueueRowAsync(OfflineOutboxItem item)
    {
        await offlineSync.RetryAsync(item);
        await RefreshOfflineStateAsync();
    }

    internal async Task DiscardQueueRowAsync(OfflineOutboxItem item)
    {
        if (!await dialog.ConfirmDangerAsync(L["offline_discard_confirm"], L["offline_discard"])) return;
        try
        {
            await offlineSync.DiscardAsync(item);
            toast.Success(L["success"]);
        }
        catch (Exception ex)
        {
            toast.Error(ApiErrors.Describe(ex));
        }
        await RefreshOfflineStateAsync();
    }

    internal async Task CancelQueueRowAsync(OfflineOutboxItem item)
    {
        if (!await dialog.ConfirmDangerAsync(L["offline_cancel_confirm"], L["offline_cancel"])) return;
        if (await offlineSync.CancelAsync(item.Id)) toast.Success(L["success"]);
        await RefreshOfflineStateAsync();
    }

    private void BuildServerQr()
    {
        var url = SettingsService.Instance.ApiBaseUrl;
        try
        {
            var uri = new Uri(url);
            if (uri.IsLoopback && LanAddress() is { } ip)
                url = new UriBuilder(uri) { Host = ip }.Uri.ToString().TrimEnd('/');
        }
        catch { }
        ServerQrUrl = url;
        // cartexsrv: prefiksi mobil skanerga bu QR sayt havolasi emas, Cartex serveri ekanini bildiradi.
        ServerQr = QrService.Generate("cartexsrv:" + url);
    }

    private static string? LanAddress() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up
                && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .Select(n => n.GetIPProperties())
            .OrderByDescending(p => p.GatewayAddresses.Count > 0)
            .SelectMany(p => p.UnicastAddresses)
            .Select(a => a.Address)
            .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
            ?.ToString();

    private void ApplyState(OfflineCacheStateDto state)
    {
        OfflineAssignedElsewhere = state.DeviceId is not null && state.DeviceId != SettingsService.Instance.DeviceId;
        OnPropertyChanged(nameof(CanEnableOffline));
        OfflineHolderText = state.DeviceId is null
            ? L["offline_cache_free"]
            : state.DeviceId == SettingsService.Instance.DeviceId
                ? $"{L["offline_cache_this"]} · {state.WarehouseName}"
                : $"{L["offline_cache_holder"]}: {state.DeviceName} · {state.WarehouseName}" +
                  (state.LastReportedPendingCount > 0 ? $" · {L["offline_cache_queue"]}: {state.LastReportedPendingCount}" : "");
    }

    partial void OnOfflineEnabledChanged(bool value) => OnPropertyChanged(nameof(CanEnableOffline));

    [RelayCommand]
    private async Task ReleaseRemoteOfflineAsync()
    {
        if (!OfflineAssignedElsewhere) return;
        if (!await dialog.ConfirmDangerAsync(L["offline_cache_remote_release_confirm"], L["disconnect"])) return;
        try
        {
            await offlineApi.ReleaseAsync(new ReleaseOfflineCacheRequest(
                _offlineState?.LeaseId, Force: true, Reason: "Boshqa qurilmadan majburan uzildi"));
            toast.Success(L["success"]);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            toast.Error(ApiErrors.Describe(ex));
        }
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
        var orphans = await offlineSync.OrphanCountAsync();
        OfflineSyncText = $"{L["offline_cache_synced"]}: {last} • {L["offline_cache_queue"]}: {pending}" +
                          (errors > 0 ? $" • {L["offline_cache_errors"]}: {errors}" : "") +
                          (orphans > 0 ? $" • {L["offline_queue_orphan"]}: {orphans}" : "");
        RefreshHubStatus();
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
                var grant = await offlineApi.ClaimAsync(new ClaimOfflineCacheRequest(
                    SettingsService.Instance.DeviceId,
                    Environment.MachineName,
                    SelectedOfflineWarehouse.Id));
                await offlineSync.ActivateAsync(grant, SelectedOfflineWarehouse);
                OfflineEnabled = offlineSync.IsEnabled;
                // HUB-04: yangi vakolat — yangi `epoch`; eski guvohnoma bilan yo'ldoshlar bu
                // qurilmani eskirgan HUB deb rad etardi.
                await hubHost.RefreshAttestationAsync();
                toast.Success(L["success"]);
            }
            else
            {
                var pending = await offlineSync.PendingCountAsync();
                var errors = await offlineSync.ErrorCountAsync();
                if (pending + errors > 0 && !await dialog.ConfirmDangerAsync(
                        $"{L["offline_cache_queue"]}: {pending + errors}. {L["offline_cache_remote_release_confirm"]}",
                        L["disconnect"]))
                    return;
                await offlineSync.ReleaseAsync(pending + errors > 0
                    ? "Sinxronlanmagan amallar bilan foydalanuvchi tomonidan uzildi"
                    : null);
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
