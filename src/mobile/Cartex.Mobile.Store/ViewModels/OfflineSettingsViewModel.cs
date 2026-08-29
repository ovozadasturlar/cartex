using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.Hub;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;
using Cartex.Shared.Models.OfflineCache;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.Mobile.Store.ViewModels;

public sealed record OfflineQueueRow(MobileOfflineOutbox Item, string KindText, bool CanVoid)
{
    public string Title { get; } = $"{KindText} · {Item.OccurredAt.ToLocalTime():dd.MM HH:mm}";
    public string? Error { get; } = Item.Error;
    public bool IsError { get; } = Item.Status == "error";
    public bool CanCancel { get; } = CanVoid && Item.Status == "pending" && Item.PushedAt is null;
}

public partial class OfflineSettingsViewModel(
    MobileAuthService auth,
    WarehouseContext warehouseContext,
    MobileOfflineService offline,
    IOfflineCacheApi offlineApi,
    AccessState access,
    MobileHubHostService hubHost) : ObservableObject
{
    [ObservableProperty] private bool _offlineEnabled;
    [ObservableProperty] private bool _offlineBusy;
    [ObservableProperty] private string _offlineStatus = "";
    [ObservableProperty] private bool _hasQueueRows;
    [ObservableProperty] private bool _importVisible;
    [ObservableProperty] private bool _capSales = true;
    [ObservableProperty] private bool _capPayments = true;
    [ObservableProperty] private bool _capSupplies = true;
    [ObservableProperty] private string _hubStatus = "";
    [ObservableProperty] private ImageSource? _hubQrSource;

    // HUB-01: xizmatni faqat vakolat egasi ocha oladi.
    public bool HubVisible => offline.IsEnabled;
    public bool HubQrVisible => HubQrSource is not null;

    public bool HubEnabled
    {
        get => hubHost.Enabled;
        set
        {
            if (hubHost.Enabled == value) return;
            hubHost.Enabled = value;
            OnPropertyChanged();
            RefreshHubStatus();
        }
    }

    private void RefreshHubStatus()
    {
        OnPropertyChanged(nameof(HubVisible));
        var endpoint = hubHost.IsServing ? hubHost.Endpoint : null;
        HubStatus = !HubEnabled
            ? Loc.Instance["hub_host_enable_hint"]
            : endpoint is not null
                ? $"{Loc.Instance["hub_serving"]}: {endpoint}"
                : Loc.Instance["hub_standby"];
        // HUB-11: yo'ldosh e'lonni eshitolmasa manzilni shu QR'dan oladi. Rasm faqat manzil
        // o'zgarganda qayta chiziladi — aks holda har yangilanishda ekranda pirillab turadi.
        if (endpoint == _qrEndpoint) return;
        _qrEndpoint = endpoint;
        HubQrSource = endpoint is null ? null : QrImage.From(HubQr.Format(endpoint));
    }

    partial void OnHubQrSourceChanged(ImageSource? value) => OnPropertyChanged(nameof(HubQrVisible));

    public ObservableCollection<OfflineQueueRow> QueueRows { get; } = [];

    private OfflineCacheStateDto? _offlineState;
    private Uri? _qrEndpoint;
    private bool _suppressToggle;

    public async Task AppearAsync()
    {
        RefreshHubStatus();
        CapSales = offline.SalesCapability;
        CapPayments = offline.PaymentsCapability;
        CapSupplies = offline.SuppliesCapability;
        ImportVisible = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
        offline.StateChanged -= OnOfflineStateChanged;
        offline.StateChanged += OnOfflineStateChanged;
        // Xizmat fonda ko'tariladi: manzil ham, QR ham darhol tayyor bo'lmaydi.
        hubHost.StateChanged -= OnHubStateChanged;
        hubHost.StateChanged += OnHubStateChanged;
        await offline.StartAsync();
        await RefreshOfflineAsync();
    }

    public void Disappear()
    {
        offline.StateChanged -= OnOfflineStateChanged;
        hubHost.StateChanged -= OnHubStateChanged;
    }

    private void OnOfflineStateChanged() =>
        MainThread.BeginInvokeOnMainThread(() => _ = RefreshQueueAsync());

    private void OnHubStateChanged() => MainThread.BeginInvokeOnMainThread(RefreshHubStatus);

    partial void OnOfflineEnabledChanged(bool value)
    {
        if (_suppressToggle) return;
        _ = ApplyToggleAsync(value);
    }

    private async Task ApplyToggleAsync(bool enable)
    {
        if (OfflineBusy) return;
        OfflineBusy = true;
        try
        {
            if (enable)
                await ClaimAuthorityAsync();
            else
                await ReleaseAuthorityAsync();
        }
        catch (Exception ex)
        {
            Ui.Toast(ex is Refit.ApiException api ? ApiErrors.Describe(api) : ex.Message);
        }
        finally
        {
            OfflineBusy = false;
        }
        await RefreshOfflineAsync();
    }

    private async Task ClaimAuthorityAsync()
    {
        _offlineState = await Task.Run(offlineApi.GetStateAsync);
        if (_offlineState.DeviceId is not null && !_offlineState.IsCurrentDevice)
        {
            var message = string.Format(Loc.Instance["offline_other_device_fmt"],
                _offlineState.DeviceName ?? "—", _offlineState.LastReportedPendingCount);
            if (!await Shell.Current.CurrentPage.DisplayAlertAsync(
                    Loc.Instance["offline_sales"], message,
                    Loc.Instance["disconnect"], Loc.Instance["cancel"]))
                return;
            await offlineApi.ReleaseAsync(new ReleaseOfflineCacheRequest(
                _offlineState.LeaseId, Force: true,
                Reason: "Mobil qurilmadan yangi vakolat olish uchun majburan uzildi"));
        }

        if (!await warehouseContext.EnsureSelectedAsync() || warehouseContext.WarehouseId is null)
            throw new InvalidOperationException(Loc.Instance["warehouse_none"]);
        var grant = await offlineApi.ClaimAsync(new ClaimOfflineCacheRequest(
            auth.DeviceId, auth.DeviceName, warehouseContext.WarehouseId.Value));
        await offline.ActivateAsync(grant);
    }

    private async Task ReleaseAuthorityAsync()
    {
        if (!offline.IsEnabled) return;
        var pending = await offline.PendingCountAsync();
        var errors = await offline.ErrorCountAsync();
        if (pending + errors > 0 && !await Shell.Current.CurrentPage.DisplayAlertAsync(
                Loc.Instance["offline_sales"],
                string.Format(Loc.Instance["offline_pending_release_fmt"], pending + errors),
                Loc.Instance["disconnect"], Loc.Instance["cancel"]))
            return;
        await offline.ReleaseAsync(pending + errors > 0
            ? "Mobil qurilmadan sinxronlanmagan amallar bilan uzildi"
            : null);
    }

    private async Task RefreshOfflineAsync()
    {
        try
        {
            _offlineState = await Task.Run(offlineApi.GetStateAsync);
            if (_offlineState.DeviceId is null)
            {
                SetToggle(false);
                OfflineStatus = Loc.Instance["offline_available"];
            }
            else if (_offlineState.IsCurrentDevice)
            {
                var pending = await offline.PendingCountAsync();
                var errors = await offline.ErrorCountAsync();
                var last = await offline.LastSyncAsync() ?? "—";
                SetToggle(offline.IsEnabled);
                OfflineStatus = string.Format(Loc.Instance["offline_status_fmt"],
                    _offlineState.WarehouseName ?? warehouseContext.WarehouseName,
                    pending, errors, last);
            }
            else
            {
                SetToggle(false);
                OfflineStatus = string.Format(Loc.Instance["offline_holder_fmt"],
                    _offlineState.DeviceName ?? "—", _offlineState.WarehouseName ?? "—",
                    _offlineState.LastReportedPendingCount);
            }
        }
        catch
        {
            SetToggle(offline.IsEnabled);
            // HUB-03: server yopiq bo'lsa telefon do'kon kassasi orqali ishlayotgan bo'lishi mumkin.
            OfflineStatus = offline.IsEnabled
                ? Loc.Instance["offline_local_ready"]
                : offline.IsSatellite
                    ? Loc.Instance["hub_satellite_hint"]
                    : Loc.Instance["err_no_connection"];
        }
        // Vakolat shu yerda olinadi yoki uziladi — HUB o'tkazgichining ko'rinishi ham shunga bog'liq.
        RefreshHubStatus();
        await RefreshQueueAsync();
    }

    // Holat serverdan o'qilganda o'tkazgich qiymati faqat ko'rsatiladi — bu yerdagi
    // o'zgarish yana vakolat olish/uzishni ishga tushirmasligi kerak.
    private void SetToggle(bool value)
    {
        _suppressToggle = true;
        OfflineEnabled = value;
        _suppressToggle = false;
    }

    partial void OnCapSalesChanged(bool value) => _ = offline.SetCapabilityAsync("sales", value);
    partial void OnCapPaymentsChanged(bool value) => _ = offline.SetCapabilityAsync("payments", value);
    partial void OnCapSuppliesChanged(bool value) => _ = offline.SetCapabilityAsync("supplies", value);

    [RelayCommand]
    private async Task ExportQueueAsync()
    {
        try
        {
            var path = Path.Combine(FileSystem.CacheDirectory, $"cartex-navbat-{DateTime.Now:yyyyMMdd-HHmm}.json");
            int count;
            await using (var stream = File.Create(path))
                count = await offline.ExportQueueAsync(stream);
            await Share.Default.RequestAsync(new ShareFileRequest(Loc.Instance["offline_export"], new ShareFile(path)));
            Ui.Toast($"{Loc.Instance["offline_export_done"]}: {count}");
        }
        catch (Exception ex)
        {
            Ui.Toast(ex.Message);
        }
    }

    [RelayCommand]
    private async Task ImportQueueAsync()
    {
        try
        {
            var picked = await FilePicker.Default.PickAsync(new PickOptions
            {
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    [DevicePlatform.Android] = ["application/json"]
                })
            });
            if (picked is null) return;
            MobileOfflineExportFile file;
            await using (var stream = await picked.OpenReadAsync())
                file = await MobileOfflineService.ParseImportFileAsync(stream);
            await Shell.Current.GoToAsync("offline-import", new Dictionary<string, object> { ["file"] = file });
        }
        catch (Exception ex)
        {
            Ui.Toast(ex is Refit.ApiException api ? ApiErrors.Describe(api) : ex.Message);
        }
    }

    // Navbat vakolat holatiga qarab yashirilmaydi: yo'ldosh rejimida yozilgan yuborilmagan
    // qatorlar vakolat ko'chganda ham shu ro'yxatda ko'rinib turishi kerak.
    private async Task RefreshQueueAsync()
    {
        var canVoid = access.CanVoidSale;
        var rows = await offline.GetQueueRowsAsync();
        QueueRows.Clear();
        foreach (var row in rows)
            QueueRows.Add(new OfflineQueueRow(row, KindLabel(row.Kind), canVoid));
        HasQueueRows = QueueRows.Count > 0;
    }

    internal static string KindLabel(string kind) => kind switch
    {
        "customer.payment.create" => Loc.Instance["offline_kind_payment"],
        "supply.create" => Loc.Instance["offline_kind_supply"],
        _ => Loc.Instance["offline_kind_sale"]
    };

    [RelayCommand]
    private async Task RetryQueueRowAsync(OfflineQueueRow row)
    {
        await offline.RetryAsync(row.Item);
        await RefreshQueueAsync();
    }

    [RelayCommand]
    private async Task DiscardQueueRowAsync(OfflineQueueRow row)
    {
        if (!await Shell.Current.CurrentPage.DisplayAlertAsync(
                Loc.Instance["offline_discard"], Loc.Instance["offline_discard_confirm"],
                Loc.Instance["offline_discard"], Loc.Instance["cancel"]))
            return;
        try
        {
            await offline.DiscardAsync(row.Item);
        }
        catch (Exception ex)
        {
            Ui.Toast(ex is Refit.ApiException api ? ApiErrors.Describe(api) : Loc.Instance["err_no_connection"]);
        }
        await RefreshQueueAsync();
    }

    [RelayCommand]
    private async Task CancelQueueRowAsync(OfflineQueueRow row)
    {
        if (!await Shell.Current.CurrentPage.DisplayAlertAsync(
                Loc.Instance["offline_cancel"], Loc.Instance["offline_cancel_confirm"],
                Loc.Instance["offline_cancel"], Loc.Instance["cancel"]))
            return;
        await offline.CancelAsync(row.Item.Id);
        await RefreshQueueAsync();
    }
}
