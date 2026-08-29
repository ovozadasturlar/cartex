using System.Security.Cryptography;
using System.Text.Json;
using Cartex.Hub;
using Cartex.Shared.Models.OfflineCache;
using Xunit;

namespace Cartex.UnitTests;

public class HubProtocolTests
{
    private const long Business = 42;
    private const long Warehouse = 7;

    private static string Sign(
        ECDsa key, string role, long businessId, string device, string publicKey, long epoch = 3) =>
        HubAttestation.Sign(key, new HubAttestationPayload
        {
            Role = role,
            BusinessId = businessId,
            DeviceId = device,
            UserId = 5,
            WarehouseId = role == HubRoles.Hub ? Warehouse : 0,
            LeaseId = role == HubRoles.Hub ? 11 : 0,
            Epoch = epoch,
            ExpiresAtUnix = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds(),
            DevicePublicKey = publicKey
        });

    private sealed class FakeBackend(HubIdentity identity) : IHubBackend
    {
        public HubIdentity Identity { get; } = identity;
        public List<OfflineSyncEventRequest> Accepted { get; } = [];
        public DateTime? LastSince { get; private set; }

        public Task<OfflineSnapshotDto> CatalogAsync(DateTime? since, CancellationToken cancellationToken)
        {
            LastSince = since;
            return Task.FromResult(new OfflineSnapshotDto("UZS", DateTime.UtcNow, [], [], [])
            {
                IsFull = since is null
            });
        }

        public Task<HubEventResult> AcceptAsync(
            OfflineSyncEventRequest request, HubAttestationPayload caller, CancellationToken cancellationToken)
        {
            Accepted.Add(request);
            return Task.FromResult(new HubEventResult(request.EventId, Accepted.Count, "Queued"));
        }
    }

    private sealed class Hub : IAsyncDisposable
    {
        private Hub(FakeBackend backend, HubServer server, HubTrust trust, ECDsa key,
            HubIdentityKey identity, Uri endpoint)
        {
            Backend = backend;
            Server = server;
            Trust = trust;
            Key = key;
            Identity = identity;
            Endpoint = endpoint;
        }

        public FakeBackend Backend { get; }
        public HubServer Server { get; }
        public HubTrust Trust { get; }
        public ECDsa Key { get; }
        public HubIdentityKey Identity { get; }
        public Uri Endpoint { get; }

        public static async Task<Hub> StartAsync()
        {
            var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
            var identity = await HubTestIdentity.CreateAsync();
            var backend = new FakeBackend(new HubIdentity(Business, Warehouse, "Asosiy ombor", 3, "Kassa-1",
                Sign(key, HubRoles.Hub, Business, "hub-device", identity.PublicKey), publicKey));
            var server = new HubServer(backend, new HubTrust(publicKey), identity.Certificate);
            server.Start(0);
            return new Hub(backend, server, new HubTrust(publicKey), key, identity,
                new Uri($"https://127.0.0.1:{server.Port}/"));
        }

        public async ValueTask DisposeAsync()
        {
            await Server.DisposeAsync();
            Identity.Dispose();
            Key.Dispose();
        }
    }

    private static async Task<(HubClient Client, HttpClient Http, HubIdentityKey Identity)> SatelliteAsync(
        Hub hub, string role = HubRoles.Satellite, long businessId = Business)
    {
        var identity = await HubTestIdentity.CreateAsync();
        var http = HubTestIdentity.Channel(identity);
        return (new HubClient(http, Sign(hub.Key, role, businessId, "phone", identity.PublicKey),
            hub.Identity.PublicKey), http, identity);
    }

    [Fact]
    public async Task Satellite_verifies_hub_and_reads_catalog()
    {
        await using var hub = await Hub.StartAsync();
        var (client, http, identity) = await SatelliteAsync(hub);
        using var _ = http;
        using var _k = identity;

        var candidate = await HubDiscovery.TryEndpointAsync(http, hub.Trust, Business,
            Sign(hub.Key, HubRoles.Satellite, Business, "phone", identity.PublicKey),
            hub.Endpoint, 0, TestContext.Current.CancellationToken);
        Assert.NotNull(candidate);
        Assert.Equal(Warehouse, candidate.Hello.WarehouseId);
        Assert.Equal("Asosiy ombor", candidate.Hello.WarehouseName);

        var full = await client.CatalogAsync(hub.Endpoint, null, TestContext.Current.CancellationToken);
        Assert.NotNull(full);
        Assert.True(full.IsFull);

        var since = new DateTime(2026, 8, 20, 10, 30, 0, DateTimeKind.Utc);
        var delta = await client.CatalogAsync(hub.Endpoint, since, TestContext.Current.CancellationToken);
        Assert.NotNull(delta);
        Assert.False(delta.IsFull);
        Assert.Equal(since, hub.Backend.LastSince);
    }

    [Fact]
    public async Task Event_reaches_the_hub_queue_unchanged()
    {
        await using var hub = await Hub.StartAsync();
        var (client, http, identity) = await SatelliteAsync(hub);
        using var _ = http;
        using var _k = identity;

        var eventId = Guid.NewGuid();
        var request = new OfflineSyncEventRequest(eventId, 0, "sale.create", "idem-1",
            new DateTime(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc),
            JsonSerializer.SerializeToElement(new { warehouseId = Warehouse, paidCash = 15000m }));

        var result = await client.SendAsync(hub.Endpoint, request, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(eventId, result.EventId);
        var stored = Assert.Single(hub.Backend.Accepted);
        Assert.Equal("sale.create", stored.Kind);
        Assert.Equal("idem-1", stored.IdempotencyKey);
        Assert.Equal(15000m, stored.Payload.GetProperty("paidCash").GetDecimal());
    }

    [Fact]
    public async Task Foreign_business_attestation_is_refused()
    {
        await using var hub = await Hub.StartAsync();
        var (client, http, identity) = await SatelliteAsync(hub, businessId: 999);
        using var _ = http;
        using var _k = identity;

        var catalog = await client.CatalogAsync(hub.Endpoint, null, TestContext.Current.CancellationToken);

        Assert.Null(catalog);
        Assert.Empty(hub.Backend.Accepted);
    }

    [Fact]
    public async Task Attestation_from_another_server_is_refused()
    {
        await using var hub = await Hub.StartAsync();
        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var identity = await HubTestIdentity.CreateAsync();
        using var http = HubTestIdentity.Channel(identity);
        var client = new HubClient(http,
            Sign(other, HubRoles.Satellite, Business, "phone", identity.PublicKey), hub.Identity.PublicKey);

        Assert.Null(await client.CatalogAsync(hub.Endpoint, null, TestContext.Current.CancellationToken));
        Assert.Empty(hub.Backend.Accepted);
    }

    // HUB-04: guvohnoma bearer emas — uni ushlab olgan qurilma o'z kaliti bilan kira olmaydi.
    [Fact]
    public async Task Stolen_attestation_used_with_another_key_is_refused()
    {
        await using var hub = await Hub.StartAsync();
        using var victim = await HubTestIdentity.CreateAsync();
        using var thief = await HubTestIdentity.CreateAsync();
        using var http = HubTestIdentity.Channel(thief);
        var stolen = Sign(hub.Key, HubRoles.Satellite, Business, "phone", victim.PublicKey);
        var client = new HubClient(http, stolen, hub.Identity.PublicKey);

        Assert.Null(await client.CatalogAsync(hub.Endpoint, null, TestContext.Current.CancellationToken));
        Assert.Null(await client.SendAsync(hub.Endpoint,
            new OfflineSyncEventRequest(Guid.NewGuid(), 0, "sale.create", "idem-1", DateTime.UtcNow,
                JsonSerializer.SerializeToElement(new { warehouseId = Warehouse })),
            TestContext.Current.CancellationToken));
        Assert.Empty(hub.Backend.Accepted);
    }

    // HUB-04: guvohnomasi kalitsiz (eski shakldagi) qurilma ham kira olmaydi.
    [Fact]
    public async Task Attestation_without_a_device_key_is_refused()
    {
        await using var hub = await Hub.StartAsync();
        using var identity = await HubTestIdentity.CreateAsync();
        using var http = HubTestIdentity.Channel(identity);
        var client = new HubClient(http,
            Sign(hub.Key, HubRoles.Satellite, Business, "phone", ""), hub.Identity.PublicKey);

        Assert.Null(await client.CatalogAsync(hub.Endpoint, null, TestContext.Current.CancellationToken));
        Assert.Empty(hub.Backend.Accepted);
    }

    // HUB-04: salom ham himoyalangan — HUB tokeni autentifikatsiyasiz berilmaydi.
    [Fact]
    public async Task Hello_without_an_attestation_is_refused()
    {
        await using var hub = await Hub.StartAsync();
        using var identity = await HubTestIdentity.CreateAsync();
        using var http = HubTestIdentity.Channel(identity);

        using var message = new HttpRequestMessage(HttpMethod.Get, new Uri(hub.Endpoint, "hub/hello"));
        message.Options.Set(HubTransport.ExpectedKey, hub.Identity.PublicKey);
        using var response = await http.SendAsync(message, TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void Expired_attestation_does_not_verify()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var trust = new HubTrust(Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()));
        var expired = HubAttestation.Sign(key, new HubAttestationPayload
        {
            Role = HubRoles.Satellite,
            BusinessId = Business,
            DeviceId = "phone",
            ExpiresAtUnix = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds()
        });

        Assert.Null(trust.Verify(expired, Business));
    }

    [Fact]
    public void Satellite_token_is_not_accepted_as_a_hub()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var trust = new HubTrust(Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()));

        Assert.Null(trust.VerifyHub(Sign(key, HubRoles.Satellite, Business, "phone", "pk"), Business));
        Assert.NotNull(trust.VerifyHub(Sign(key, HubRoles.Hub, Business, "hub-device", "pk"), Business));
    }

    [Fact]
    public void Tampered_payload_breaks_the_signature()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var trust = new HubTrust(Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()));
        var token = Sign(key, HubRoles.Satellite, Business, "phone", "pk");
        var tampered = "a" + token[1..];

        Assert.Null(trust.Verify(tampered, Business));
    }

    // HUB-04: guvohnomadagi kalit qurilma sertifikatidagi kalit bilan bir xil bo'lishi shart.
    [Fact]
    public async Task Device_certificate_carries_the_attested_key()
    {
        using var identity = await HubTestIdentity.CreateAsync();
        using var other = await HubTestIdentity.CreateAsync();

        Assert.True(HubIdentityKey.Matches(identity.Certificate, identity.PublicKey));
        Assert.False(HubIdentityKey.Matches(identity.Certificate, other.PublicKey));
        Assert.False(HubIdentityKey.Matches(identity.Certificate, ""));
        Assert.False(HubIdentityKey.Matches(certificate: null, identity.PublicKey));
    }

    // Kalit saqlangan joydan qayta o'qiladi: qurilma qayta ishga tushsa ham o'sha kalitda qoladi.
    [Fact]
    public async Task Stored_identity_survives_a_restart()
    {
        var store = new MemoryIdentityStore();
        using var first = await HubIdentityKey.LoadAsync(store, TestContext.Current.CancellationToken);
        using var second = await HubIdentityKey.LoadAsync(store, TestContext.Current.CancellationToken);

        Assert.Equal(first.PublicKey, second.PublicKey);
        Assert.Equal(first.Certificate.RawData, second.Certificate.RawData);
    }
}
