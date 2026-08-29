using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cartex.Hub;
using Cartex.Shared.Models.OfflineCache;
using Cartex.Shared.Models.Stocks;

namespace Cartex.UI.Services;

/// HUB-01/HUB-02: vakolat egasi bulutga ulana olmay qolganda do'kon tarmog'ida xizmat ochadi.
/// Bulut qaytishi bilan xizmat yopiladi — qurilmalar o'zi bulutga qaytadi.
public sealed class HubHostService(
    HubClientService hubClient,
    HubIdentityService identity,
    OfflineStore store,
    OfflineSyncService sync,
    ConnectivityService connectivity,
    AuthService auth)
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private HubServer? _server;
    private HubBeacon? _beacon;
    private bool _hooked;

    public bool IsServing => _server?.IsRunning == true;
    public bool Error { get; private set; }
    public string? Endpoint { get; private set; }
    public event Action? StateChanged;

    /// HUB-11: manzil xizmat ochilmagan paytda ham kerak — QR bir marta skanerlanadi,
    /// yo'ldosh esa keyin, bulut yopilganda o'sha manzilga qaytadi.
    public Uri? LinkEndpoint =>
        Endpoint is { } serving
            ? new Uri(serving)
            : HubNetwork.LocalAddress() is { } address
                ? new Uri($"https://{address}:{HubServer.DefaultPort}/")
                : null;

    public void Start()
    {
        if (_hooked) return;
        _hooked = true;
        connectivity.PropertyChanged += OnConnectivityChanged;
        auth.LoggedOut += OnLoggedOut;
        _ = ApplyAsync();
    }

    public async Task RefreshAttestationAsync()
    {
        if (!connectivity.IsOnline) return;
        await hubClient.RefreshAttestationAsync();
    }

    public async Task ApplyAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (ShouldServe())
                await StartServingAsync();
            else
                await StopServingAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    private bool ShouldServe() =>
        SettingsService.Instance.HubEnabled
        && auth.IsAuthenticated
        && sync.IsEnabled
        && !connectivity.IsOnline
        && hubClient.Credential is { Token.Length: > 0, ServerKey.Length: > 0 };

    private async Task StartServingAsync()
    {
        if (IsServing) return;
        var credential = sync.Credential;
        if (credential is null || hubClient.Credential is not { } attestation) return;

        // HUB-04: o'z guvohnomasi haqiqiy bo'lmasa va u shu qurilma kalitiga bog'lanmagan bo'lsa
        // xizmat ochilmaydi — yo'ldosh TLS'da kalit mos kelmagani uchun baribir rad etardi.
        var trust = new HubTrust(attestation.ServerKey);
        var key = await identity.KeyAsync();
        if (HubAttestation.Verify(attestation.Token, attestation.ServerKey, DateTime.UtcNow) is not { } payload
            || trust.VerifyHub(attestation.Token, payload.BusinessId, credential.WarehouseId) is not { } attested
            || !HubIdentityKey.SameKey(attested.DevicePublicKey, key.PublicKey))
            return;

        var backend = new DesktopHubBackend(store, () => sync.Credential, attestation.Token, trust.PublicKey,
            await store.GetMetaAsync("offline_warehouse_name") ?? "", Environment.MachineName);
        _server = new HubServer(backend, trust, key.Certificate);
        try
        {
            _server.Start();
        }
        catch (SocketException)
        {
            // Port band (boshqa nusxa ishlayapti yoki tarmoq cheklovi) — xizmat ochilmaydi,
            // vakolat egasining o'zi esa odatdagidek oflayn ishlashda davom etadi.
            _server = null;
            Error = true;
            StateChanged?.Invoke();
            return;
        }
        Error = false;
        var address = HubNetwork.LocalAddress();
        Endpoint = address is null ? null : $"https://{address}:{_server.Port}/";
        _beacon = new HubBeacon();
        var port = _server.Port;
        _beacon.StartAnnouncing(() => new HubBeaconDto(
            backend.Identity.BusinessId, backend.Identity.WarehouseId, backend.Identity.Epoch,
            backend.Identity.DeviceName, port));
        StateChanged?.Invoke();
    }

    private async Task StopServingAsync()
    {
        if (_beacon is not null) await _beacon.StopAsync();
        if (_server is not null) await _server.StopAsync();
        _beacon = null;
        _server = null;
        Endpoint = null;
        StateChanged?.Invoke();
    }

    private void OnConnectivityChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ConnectivityService.IsOnline)) return;
        if (connectivity.IsOnline && auth.IsAuthenticated) _ = RefreshAttestationAsync();
        _ = ApplyAsync();
    }

    private void OnLoggedOut() => _ = ApplyAsync();
}

/// HUB-09/HUB-06: katalog vakolat egasining keshidan, hodisa esa uning navbatiga.
public sealed class DesktopHubBackend(
    OfflineStore store,
    Func<OfflineLeaseCredential?> credential,
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
            var lease = credential();
            return new HubIdentity(
                payload?.BusinessId ?? 0,
                lease?.WarehouseId ?? 0,
                warehouseName,
                lease?.Epoch ?? 0,
                deviceName,
                attestationToken,
                publicKey);
        }
    }

    /// HUB-09: LAN tez, yo'ldosh keshi esa oyna tugashi bilan tashlanadi — shuning uchun
    /// har safar to'liq nusxa beriladi va delta murakkabligi klientga o'tkazilmaydi.
    public async Task<OfflineSnapshotDto> CatalogAsync(DateTime? since, CancellationToken cancellationToken)
    {
        var (products, barcodes, customers, suppliers) = await store.GetSnapshotAsync();
        var lease = credential();
        return new OfflineSnapshotDto(
            await store.GetMetaAsync("base_currency") ?? "UZS",
            DateTime.UtcNow,
            [.. products.Select(x => new StockOnHandDto(x.VariantId, x.ProductName, null, x.CategoryName,
                x.UnitName, "", x.Quantity, x.SellingPrice, null, null, null, null, null,
                x.AllowsAmountEntry, x.AllowsFractional))],
            [.. barcodes.Select(x => new OfflineBarcodeDto(x.VariantId, x.Code, x.PackQty))],
            [.. customers.Select(x => new OfflineCustomerDto(x.Id, x.FullName, x.Phone, x.CardBarcode,
                x.DiscountPct, x.DebtBalance, x.CreditLimit))],
            await store.GetAllowDebtSalesAsync(),
            await store.GetAllowInsufficientStockSalesAsync(),
            lease?.LeaseId ?? 0,
            lease?.Epoch ?? 0,
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
        var lease = credential();
        if (lease is null)
            return new HubEventResult(request.EventId, 0, "Rejected", "hub_lease_missing", "Vakolat yo'q.");

        // HUB-10: qoldiq faqat vakolat omborida hisoblanadi — boshqa ombor hodisasi qabul qilinsa,
        // proyeksiya noto'g'ri omborni kamaytirib, ikkala qoldiqni ham buzardi.
        if (request.Payload.TryGetProperty("warehouseId", out var warehouse)
            && warehouse.TryGetInt64(out var warehouseId)
            && warehouseId != lease.WarehouseId)
            return new HubEventResult(request.EventId, 0, "Rejected", "hub_warehouse_mismatch",
                "Amal boshqa omborga tegishli.");

        try
        {
            var item = await store.EnqueueFromSatelliteAsync(
                request.Kind,
                JsonSerializer.Serialize(request.Payload, Json),
                request.IdempotencyKey,
                request.EventId,
                request.OccurredAt,
                lease,
                // HUB-05: muallif guvohnomadan olinadi — tanadagi qiymatga ishonilmaydi.
                caller.UserId,
                await store.GetAllowInsufficientStockSalesAsync());
            return new HubEventResult(request.EventId, item.Sequence, "Queued");
        }
        catch (InvalidOperationException exception)
        {
            return new HubEventResult(request.EventId, 0, "Rejected", "hub_rejected", exception.Message);
        }
    }
}
