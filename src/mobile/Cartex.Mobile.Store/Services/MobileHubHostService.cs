using System.Text.Json;
using Cartex.Hub;
using Cartex.Mobile.Core;
using Cartex.Shared.Models.OfflineCache;
using Cartex.Shared.Models.Stocks;

namespace Cartex.Mobile.Store.Services;

// HUB-01: vakolat telefonda bo'lsa, xizmatni ham telefon ochadi — rol qurilma turiga bog'liq emas.
public sealed class MobileHubHostService(
    MobileOfflineStore store,
    MobileOfflineService offline,
    HubLinkService link,
    HubIdentityService identity,
    WarehouseContext warehouse,
    MobileAuthService auth)
{
    private const string EnabledKey = "hub_host_enabled";
    private readonly SemaphoreSlim _lock = new(1, 1);
    private HubServer? _server;
    private HubBeacon? _beacon;

    public event Action? StateChanged;

    public bool IsServing => _server?.IsRunning == true;
    public Uri? Endpoint { get; private set; }

    public bool Enabled
    {
        get => Preferences.Get(EnabledKey, false);
        set
        {
            if (Enabled == value) return;
            Preferences.Set(EnabledKey, value);
            _ = ApplyAsync();
        }
    }

    private bool _hooked;

    // Xizmat oflayn holat va tarmoq o'zgarishiga ergashadi — foydalanuvchi hech narsa bosmaydi.
    public void Start()
    {
        if (_hooked) return;
        _hooked = true;
        offline.StateChanged += () => _ = ApplyAsync();
        Connectivity.Current.ConnectivityChanged += (_, _) => _ = ApplyAsync();
        _ = ApplyAsync();
    }

    public async Task ApplyAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (ShouldServe()) await StartAsync();
            else await StopAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    private bool ShouldServe() =>
        auth.UserId is not null
        && Enabled
        && offline.IsEnabled
        && offline.ShouldUseOffline
        && link.Trust is not null
        && link.AttestationToken is { Length: > 0 };

    private async Task StartAsync()
    {
        if (IsServing) return;
        if (offline.Credential is not { } credential || link.Trust is not { } trust) return;
        var token = link.AttestationToken;
        var key = await identity.KeyAsync();

        // HUB-04: guvohnoma shu qurilma kalitiga bog'lanmagan bo'lsa xizmat ochilmaydi — yo'ldosh
        // TLS'da kalit mos kelmagani uchun baribir rad etardi.
        if (trust.VerifyHub(token, link.BusinessId ?? 0, credential.WarehouseId) is not { } attested
            || !HubIdentityKey.SameKey(attested.DevicePublicKey, key.PublicKey))
            return;

        var backend = new MobileHubBackend(store, offline, token!, trust.PublicKey,
            warehouse.WarehouseName, DeviceInfo.Current.Name);
        _server = new HubServer(backend, trust, key.Certificate);
        try
        {
            _server.Start();
        }
        catch (Exception)
        {
            _server = null;
            StateChanged?.Invoke();
            return;
        }

        var address = HubNetwork.LocalAddress();
        Endpoint = address is null ? null : new Uri($"https://{address}:{_server.Port}/");
        _beacon = new HubBeacon();
        var port = _server.Port;
        _beacon.StartAnnouncing(() => new HubBeaconDto(
            backend.Identity.BusinessId, backend.Identity.WarehouseId, backend.Identity.Epoch,
            backend.Identity.DeviceName, port));
        HubForegroundControl.Start?.Invoke();
        StateChanged?.Invoke();
    }

    private async Task StopAsync()
    {
        if (_beacon is not null) await _beacon.StopAsync();
        if (_server is not null) await _server.StopAsync();
        _beacon = null;
        _server = null;
        Endpoint = null;
        HubForegroundControl.Stop?.Invoke();
        StateChanged?.Invoke();
    }
}

// Androidda ekran o'chganda jarayon to'xtatiladi va tinglovchi jim bo'lib qoladi; platforma
// bu ikki chaqiruv orqali fon xizmatini yoqadi. Boshqa platformalarda bo'sh qoladi.
public static class HubForegroundControl
{
    public static Action? Start { get; set; }
    public static Action? Stop { get; set; }
}

public sealed class MobileHubBackend(
    MobileOfflineStore store,
    MobileOfflineService offline,
    string attestationToken,
    string publicKey,
    string warehouseName,
    string deviceName) : IHubBackend
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public HubIdentity Identity
    {
        get
        {
            var payload = HubAttestation.Verify(attestationToken, publicKey, DateTime.UtcNow);
            var credential = offline.Credential;
            return new HubIdentity(
                payload?.BusinessId ?? 0,
                credential?.WarehouseId ?? 0,
                warehouseName,
                credential?.Epoch ?? 0,
                deviceName,
                attestationToken,
                publicKey);
        }
    }

    public async Task<OfflineSnapshotDto> CatalogAsync(DateTime? since, CancellationToken cancellationToken)
    {
        var (products, barcodes, customers, suppliers) = await store.GetSnapshotAsync();
        var credential = offline.Credential;
        return new OfflineSnapshotDto(
            await store.GetMetaAsync("base_currency") ?? "UZS",
            DateTime.UtcNow,
            [.. products.Select(x => new StockOnHandDto(x.VariantId, x.ProductName, null, x.CategoryName,
                x.UnitName, "", x.Quantity, x.SellingPrice, null, null, null, null, null,
                x.AllowsAmountEntry, x.AllowsFractional))],
            [.. barcodes.Select(x => new OfflineBarcodeDto(x.VariantId, x.Code, x.PackQty))],
            [.. customers.Select(x => new OfflineCustomerDto(x.Id, x.FullName, x.Phone, x.CardBarcode,
                x.DiscountPct, x.DebtBalance, x.CreditLimit))],
            await store.GetMetaAsync("allow_debt_sales") != "0",
            await store.GetMetaAsync("allow_insufficient_stock_sales") == "1",
            credential?.LeaseId ?? 0,
            credential?.Epoch ?? 0,
            0,
            null,
            null,
            [.. suppliers.Select(x => new OfflineSupplierDto(x.Id, x.Name, x.Phone))])
        {
            IsFull = true,
            Totals = new OfflineSnapshotTotals(products.Count, barcodes.Count, customers.Count, suppliers.Count)
        };
    }

    public async Task<HubEventResult> AcceptAsync(
        OfflineSyncEventRequest request, HubAttestationPayload caller, CancellationToken cancellationToken)
    {
        if (offline.Credential is not { } credential)
            return new HubEventResult(request.EventId, 0, "Rejected", "hub_lease_missing", "Vakolat yo'q.");

        // HUB-10: qoldiq faqat vakolat omborida hisoblanadi.
        if (request.Payload.TryGetProperty("warehouseId", out var warehouse)
            && warehouse.TryGetInt64(out var warehouseId)
            && warehouseId != credential.WarehouseId)
            return new HubEventResult(request.EventId, 0, "Rejected", "hub_warehouse_mismatch",
                "Amal boshqa omborga tegishli.");

        try
        {
            var sequence = await store.EnqueueFromSatelliteAsync(
                request.Kind,
                JsonSerializer.Serialize(request.Payload, Json),
                request.IdempotencyKey,
                request.EventId,
                request.OccurredAt,
                credential,
                // HUB-05: muallif guvohnomadan olinadi, tanadagi qiymatdan emas.
                caller.UserId);
            return new HubEventResult(request.EventId, sequence, "Queued");
        }
        catch (InvalidOperationException exception)
        {
            return new HubEventResult(request.EventId, 0, "Rejected", "hub_rejected", exception.Message);
        }
    }
}
