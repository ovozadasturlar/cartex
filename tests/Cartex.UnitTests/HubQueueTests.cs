using System.Net.Security;
using System.Security.Cryptography;
using System.Text.Json;
using Cartex.Hub;
using Cartex.Shared.Models.OfflineCache;
using Cartex.Shared.Models.Sales;
using Cartex.UI.Services;
using SQLite;
using Xunit;

namespace Cartex.UnitTests;

public class HubQueueTests
{
    private const long Business = 42;
    private const long Warehouse = 7;
    private const long OtherWarehouse = 8;
    private const long Epoch = 3;
    private const long LeaseId = 11;
    private const long HubUser = 5;
    private const long SatelliteUser = 88;
    private const long Variant = 101;
    private const decimal Price = 12500m;
    private const decimal StockOnHand = 3m;
    private const string HubDevice = "kassa-1";
    private const string SatelliteDevice = "phone-1";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateTime OccurredAt = new(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);

    private static string Sign(
        ECDsa key,
        string role,
        string devicePublicKey,
        long businessId = Business,
        long warehouseId = 0,
        long epoch = 0,
        long userId = SatelliteUser,
        string device = SatelliteDevice,
        int expiresInDays = 30) =>
        HubAttestation.Sign(key, new HubAttestationPayload
        {
            Role = role,
            BusinessId = businessId,
            DeviceId = device,
            UserId = userId,
            WarehouseId = warehouseId,
            LeaseId = role == HubRoles.Hub ? LeaseId : 0,
            Epoch = epoch,
            ExpiresAtUnix = DateTimeOffset.UtcNow.AddDays(expiresInDays).ToUnixTimeSeconds(),
            DevicePublicKey = devicePublicKey
        });

    private static CreateSaleRequest Sale(long warehouseId, decimal quantity = 1m) =>
        new(warehouseId, null, Price * quantity, 0m, 0m, [new CreateSaleItemRequest(Variant, quantity, Price)]);

    private static OfflineSyncEventRequest SatelliteSale(
        Guid eventId, string idempotencyKey, long warehouseId, decimal quantity = 1m, long? actorUserId = null) =>
        new(eventId, 41, "sale.create", idempotencyKey, OccurredAt,
            JsonSerializer.SerializeToElement(Sale(warehouseId, quantity), HubJson.Options), actorUserId);

    private sealed class RealHub : IAsyncDisposable
    {
        private readonly HubServer _server;
        private readonly HttpClient _http;
        private readonly string _directory;
        private readonly HubIdentityKey _hubIdentity;
        private readonly HubIdentityKey _satelliteIdentity;

        private RealHub(string directory, OfflineStore store, OfflineLeaseCredential credential,
            ECDsa key, HubTrust trust, string satelliteToken, HubServer server,
            HubIdentityKey hubIdentity, HubIdentityKey satelliteIdentity)
        {
            _directory = directory;
            _server = server;
            _hubIdentity = hubIdentity;
            _satelliteIdentity = satelliteIdentity;
            _http = HubTestIdentity.Channel(satelliteIdentity);
            Store = store;
            Credential = credential;
            Key = key;
            Trust = trust;
            SatelliteToken = satelliteToken;
            Endpoint = new Uri($"https://127.0.0.1:{server.Port}/");
        }

        public OfflineStore Store { get; }
        public OfflineLeaseCredential Credential { get; }
        public ECDsa Key { get; }
        public HubTrust Trust { get; }
        public string SatelliteToken { get; }
        public Uri Endpoint { get; }
        public string HubPublicKey => _hubIdentity.PublicKey;
        public string SatellitePublicKey => _satelliteIdentity.PublicKey;
        public HttpClient Http => _http;

        public static async Task<RealHub> StartAsync()
        {
            var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
            var directory = Path.Combine(Path.GetTempPath(), "cartex-hub-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);

            var store = new OfflineStore(Path.Combine(directory, "offline.db3"));
            var credential = new OfflineLeaseCredential(HubDevice, LeaseId, Warehouse, Epoch, "lease-token", 0);
            await store.PrepareLeaseAsync(credential);
            await store.ReplaceSnapshotAsync(
                [
                    new OfflineProduct
                    {
                        VariantId = Variant,
                        ProductName = "Sut 1L",
                        CategoryName = "Sut mahsulotlari",
                        UnitName = "dona",
                        Quantity = StockOnHand,
                        SellingPrice = Price
                    }
                ],
                [new OfflineBarcode { Code = "4780001", VariantId = Variant, PackQty = 1m }],
                [
                    new OfflineCustomer
                    {
                        Id = 9,
                        FullName = "Ali Valiev",
                        Phone = "+998901112233",
                        CardBarcode = "CARD-9",
                        DiscountPct = 5m,
                        DebtBalance = 20000m,
                        CreditLimit = 500000m
                    }
                ],
                [],
                LeaseId, Epoch, snapshotVersion: 4);

            var hubIdentity = await HubTestIdentity.CreateAsync();
            var satelliteIdentity = await HubTestIdentity.CreateAsync();
            var backend = new DesktopHubBackend(store, () => credential,
                Sign(key, HubRoles.Hub, hubIdentity.PublicKey, warehouseId: Warehouse, epoch: Epoch,
                    userId: HubUser, device: HubDevice),
                publicKey, "Asosiy ombor", "Kassa-1");
            var trust = new HubTrust(publicKey);
            var server = new HubServer(backend, trust, hubIdentity.Certificate);
            server.Start(0);

            return new RealHub(directory, store, credential, key, trust,
                Sign(key, HubRoles.Satellite, satelliteIdentity.PublicKey), server,
                hubIdentity, satelliteIdentity);
        }

        public HubClient Satellite(string? token = null) => new(_http, token ?? SatelliteToken, HubPublicKey);

        public Task<List<OfflineOutboxItem>> OutboxAsync() => Store.GetOutboxAsync(LeaseId, Epoch);

        public async Task<decimal> StockAsync() => (await Store.GetProductAsync(Variant))!.Quantity;

        public async ValueTask DisposeAsync()
        {
            await _server.DisposeAsync();
            _http.Dispose();
            _hubIdentity.Dispose();
            _satelliteIdentity.Dispose();
            Key.Dispose();
            SQLiteAsyncConnection.ResetPool();
            try
            {
                Directory.Delete(_directory, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    // HUB-06
    [Fact]
    public async Task Satellite_event_joins_the_hub_queue_with_the_hub_sequence()
    {
        await using var hub = await RealHub.StartAsync();
        var local = await hub.Store.EnqueueSaleAndAdjustStockAsync(
            JsonSerializer.Serialize(Sale(Warehouse), HubJson.Options), "idem-hub", hub.Credential, HubUser, false);
        var eventId = Guid.NewGuid();

        var result = await hub.Satellite().SendAsync(
            hub.Endpoint, SatelliteSale(eventId, "idem-phone", Warehouse), Ct);

        Assert.NotNull(result);
        Assert.Equal(eventId, result.EventId);
        Assert.Equal(local.Sequence + 1, result.Sequence);

        var outbox = await hub.OutboxAsync();
        Assert.Equal(2, outbox.Count);
        var queued = Assert.Single(outbox, row => row.Sequence == result.Sequence);
        Assert.Equal(eventId, Guid.Parse(queued.EventId));
        Assert.Equal("sale.create", queued.Kind);
        Assert.Equal(LeaseId, queued.LeaseId);
        Assert.Equal(Epoch, queued.Epoch);
        Assert.Equal(OccurredAt, queued.OccurredAt);
    }

    // HUB-06
    [Fact]
    public async Task Repeated_event_id_does_not_create_a_second_queue_row()
    {
        await using var hub = await RealHub.StartAsync();
        var satellite = hub.Satellite();
        var sale = SatelliteSale(Guid.NewGuid(), "idem-phone", Warehouse);

        var first = await satellite.SendAsync(hub.Endpoint, sale, Ct);
        var retry = await satellite.SendAsync(hub.Endpoint, sale, Ct);

        Assert.NotNull(first);
        Assert.NotNull(retry);
        Assert.Equal(sale.EventId, retry.EventId);
        Assert.Equal(first.Sequence, retry.Sequence);
        var queued = Assert.Single(await hub.OutboxAsync());
        Assert.Equal(sale.EventId, Guid.Parse(queued.EventId));
        Assert.Equal(StockOnHand - 1m, await hub.StockAsync());
    }

    // HUB-10
    [Fact]
    public async Task Event_for_another_warehouse_is_refused()
    {
        await using var hub = await RealHub.StartAsync();

        var result = await hub.Satellite().SendAsync(
            hub.Endpoint, SatelliteSale(Guid.NewGuid(), "idem-phone", OtherWarehouse), Ct);

        Assert.Empty(await hub.OutboxAsync());
        Assert.Equal(StockOnHand, await hub.StockAsync());
        Assert.NotNull(result);
        Assert.Equal("hub_warehouse_mismatch", result.ErrorCode);
    }

    // HUB-10
    [Fact]
    public async Task Satellite_sale_lowers_the_hub_stock_once()
    {
        await using var hub = await RealHub.StartAsync();
        var satellite = hub.Satellite();

        await satellite.SendAsync(hub.Endpoint, SatelliteSale(Guid.NewGuid(), "idem-1", Warehouse, 2m), Ct);

        Assert.Equal(StockOnHand - 2m, await hub.StockAsync());
        var catalog = await satellite.CatalogAsync(hub.Endpoint, null, Ct);
        Assert.NotNull(catalog);
        Assert.Equal(StockOnHand - 2m, Assert.Single(catalog.Products).Quantity);
    }

    // HUB-05
    [Fact]
    public async Task Event_without_an_attestation_is_refused()
    {
        await using var hub = await RealHub.StartAsync();

        var result = await hub.Satellite("").SendAsync(
            hub.Endpoint, SatelliteSale(Guid.NewGuid(), "idem-phone", Warehouse), Ct);

        Assert.Null(result);
        Assert.Empty(await hub.OutboxAsync());
    }

    // HUB-05
    [Fact]
    public async Task Event_signed_by_another_server_is_refused()
    {
        await using var hub = await RealHub.StartAsync();
        using var foreignKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var result = await hub.Satellite(Sign(foreignKey, HubRoles.Satellite, hub.SatellitePublicKey)).SendAsync(
            hub.Endpoint, SatelliteSale(Guid.NewGuid(), "idem-phone", Warehouse), Ct);

        Assert.Null(result);
        Assert.Empty(await hub.OutboxAsync());
    }

    // HUB-04: ushlangan guvohnoma boshqa qurilmada ishlamaydi — TLS kaliti mos kelmaydi.
    [Fact]
    public async Task Event_from_a_device_that_does_not_hold_the_attested_key_is_refused()
    {
        await using var hub = await RealHub.StartAsync();
        using var stranger = await HubTestIdentity.CreateAsync();

        var result = await hub.Satellite(Sign(hub.Key, HubRoles.Satellite, stranger.PublicKey)).SendAsync(
            hub.Endpoint, SatelliteSale(Guid.NewGuid(), "idem-phone", Warehouse), Ct);

        Assert.Null(result);
        Assert.Empty(await hub.OutboxAsync());
    }

    // HUB-12: klient sertifikatisiz ulangan tomon hech narsa ololmaydi.
    [Fact]
    public async Task Client_without_a_certificate_is_refused()
    {
        await using var hub = await RealHub.StartAsync();
        using var handler = new SocketsHttpHandler
        {
            SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = (_, _, _, _) => true
            }
        };
        using var http = new HttpClient(handler);
        using var message = new HttpRequestMessage(HttpMethod.Get, new Uri(hub.Endpoint, "hub/catalog"));
        message.Headers.Add(HubHeaders.Attestation, hub.SatelliteToken);

        var refused = false;
        try
        {
            using var response = await http.SendAsync(message, Ct);
            refused = !response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            refused = true;
        }

        Assert.True(refused);
    }

    // HUB-05
    [Fact]
    public async Task Actor_comes_from_the_attestation_not_from_the_body()
    {
        await using var hub = await RealHub.StartAsync();

        var result = await hub.Satellite().SendAsync(
            hub.Endpoint, SatelliteSale(Guid.NewGuid(), "idem-phone", Warehouse, actorUserId: 999), Ct);

        Assert.NotNull(result);
        Assert.Equal(SatelliteUser, Assert.Single(await hub.OutboxAsync()).ActorUserId);
    }

    // HUB-04
    [Fact]
    public async Task Satellite_attestation_carries_no_warehouse_and_is_still_accepted()
    {
        await using var hub = await RealHub.StartAsync();
        var attested = hub.Trust.Verify(hub.SatelliteToken, Business);

        Assert.NotNull(attested);
        Assert.Equal(0L, attested.WarehouseId);
        Assert.Equal(SatelliteDevice, attested.DeviceId);

        var result = await hub.Satellite().SendAsync(
            hub.Endpoint, SatelliteSale(Guid.NewGuid(), "idem-phone", Warehouse), Ct);

        Assert.NotNull(result);
        Assert.Single(await hub.OutboxAsync());
    }

    // HUB-05
    [Fact]
    public async Task Satellite_reads_the_hub_epoch_from_the_signed_attestation()
    {
        await using var hub = await RealHub.StartAsync();

        var candidate = await HubDiscovery.TryEndpointAsync(hub.Http, hub.Trust, Business,
            hub.SatelliteToken, hub.Endpoint, 0, Ct);

        Assert.NotNull(candidate);
        var hello = candidate.Hello;
        var attested = hub.Trust.VerifyHub(hello.Token, Business, Warehouse);
        Assert.NotNull(attested);
        Assert.Equal(Epoch, attested.Epoch);
        Assert.Equal(hello.Epoch, attested.Epoch);
        Assert.Equal(Warehouse, hello.WarehouseId);
        Assert.Equal(hub.HubPublicKey, attested.DevicePublicKey);
        Assert.Null(hub.Trust.VerifyHub(hello.Token, Business, OtherWarehouse));
    }

    // HUB-09
    [Fact]
    public async Task Satellite_reads_exactly_what_the_hub_cache_holds()
    {
        await using var hub = await RealHub.StartAsync();

        var catalog = await hub.Satellite().CatalogAsync(hub.Endpoint, null, Ct);

        Assert.NotNull(catalog);
        var product = Assert.Single(catalog.Products);
        Assert.Equal(Variant, product.VariantId);
        Assert.Equal("Sut 1L", product.ProductName);
        Assert.Equal(StockOnHand, product.Quantity);
        Assert.Equal(Price, product.SellingPrice);
        Assert.Equal("4780001", Assert.Single(catalog.Barcodes).Code);
        Assert.Equal(20000m, Assert.Single(catalog.Customers).DebtBalance);
        Assert.Equal(new OfflineSnapshotTotals(1, 1, 1, 0), catalog.Totals);
    }

    // HUB-09
    [Fact]
    public async Task Delta_request_still_reports_the_whole_cache_totals()
    {
        await using var hub = await RealHub.StartAsync();
        var since = new DateTime(2026, 8, 20, 10, 30, 0, DateTimeKind.Utc);

        var catalog = await hub.Satellite().CatalogAsync(hub.Endpoint, since, Ct);

        Assert.NotNull(catalog);
        Assert.Equal(new OfflineSnapshotTotals(1, 1, 1, 0), catalog.Totals);
    }
}
