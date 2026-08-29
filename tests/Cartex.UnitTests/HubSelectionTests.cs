using System.Security.Cryptography;
using Cartex.Hub;
using Cartex.Shared.Models.OfflineCache;
using Xunit;

namespace Cartex.UnitTests;

public class HubSelectionTests
{
    private const long Business = 77;
    private const long Warehouse = 3;

    private sealed class StubBackend(HubIdentity identity) : IHubBackend
    {
        public HubIdentity Identity { get; } = identity;

        public Task<OfflineSnapshotDto> CatalogAsync(DateTime? since, CancellationToken cancellationToken) =>
            Task.FromResult(new OfflineSnapshotDto("UZS", DateTime.UtcNow, [], [], []));

        public Task<HubEventResult> AcceptAsync(
            OfflineSyncEventRequest request, HubAttestationPayload caller, CancellationToken cancellationToken) =>
            Task.FromResult(new HubEventResult(request.EventId, 1, "Queued"));
    }

    private static string HubToken(
        ECDsa key, long businessId, long epoch, string devicePublicKey, TimeSpan? lifetime = null) =>
        HubAttestation.Sign(key, new HubAttestationPayload
        {
            Role = HubRoles.Hub,
            BusinessId = businessId,
            DeviceId = "kassa",
            UserId = 12,
            WarehouseId = Warehouse,
            LeaseId = 90,
            Epoch = epoch,
            ExpiresAtUnix = DateTimeOffset.UtcNow.Add(lifetime ?? TimeSpan.FromDays(30)).ToUnixTimeSeconds(),
            DevicePublicKey = devicePublicKey
        });

    private static string SatelliteToken(ECDsa key, string devicePublicKey) =>
        HubAttestation.Sign(key, new HubAttestationPayload
        {
            Role = HubRoles.Satellite,
            BusinessId = Business,
            DeviceId = "telefon",
            UserId = 12,
            ExpiresAtUnix = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds(),
            DevicePublicKey = devicePublicKey
        });

    private static (HubServer Server, Uri Endpoint) StartHub(
        string publicKey, string token, long epoch, HubIdentityKey identity)
    {
        var server = new HubServer(
            new StubBackend(new HubIdentity(Business, Warehouse, "Markaziy ombor", epoch, "Kassa-1", token, publicKey)),
            new HubTrust(publicKey),
            identity.Certificate);
        server.Start(0);
        return (server, new Uri($"https://127.0.0.1:{server.Port}/"));
    }

    private static string PublicKeyOf(ECDsa key) => Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());

    private static HubCandidate Announced(string host, long announcedEpoch, long attestedEpoch) =>
        new(new Uri($"https://{host}:{HubDiscovery.Port}/"),
            new HubHelloDto("token", Warehouse, "Markaziy ombor", announcedEpoch, "Kassa-1", true),
            new HubAttestationPayload
            {
                Role = HubRoles.Hub,
                BusinessId = Business,
                WarehouseId = Warehouse,
                LeaseId = 90,
                Epoch = attestedEpoch
            });

    // HUB-11: yo'ldosh o'z do'konining e'lon qilgan HUB'ini tanlaydi.
    [Fact]
    public async Task Hub_of_the_same_business_is_selected()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKey = PublicKeyOf(key);
        using var hub = await HubTestIdentity.CreateAsync();
        using var phone = await HubTestIdentity.CreateAsync();
        var (server, endpoint) = StartHub(publicKey, HubToken(key, Business, 5, hub.PublicKey), 5, hub);
        await using var _ = server;
        using var http = HubTestIdentity.Channel(phone);

        var candidate = await HubDiscovery.TryEndpointAsync(http, new HubTrust(publicKey), Business,
            SatelliteToken(key, phone.PublicKey), endpoint, 0, TestContext.Current.CancellationToken);

        Assert.NotNull(candidate);
        Assert.Equal(endpoint, candidate.Endpoint);
        Assert.Equal(Warehouse, candidate.Attestation.WarehouseId);
        Assert.Equal(5, candidate.Attestation.Epoch);
    }

    // HUB-05: `businessId` mos kelmasa ulanish yo'q — savdo begona do'kon navbatiga tushmasin.
    [Fact]
    public async Task Hub_of_another_business_is_not_selected()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKey = PublicKeyOf(key);
        using var hub = await HubTestIdentity.CreateAsync();
        using var phone = await HubTestIdentity.CreateAsync();
        var (server, endpoint) = StartHub(publicKey, HubToken(key, Business + 1, 5, hub.PublicKey), 5, hub);
        await using var _ = server;
        using var http = HubTestIdentity.Channel(phone);

        var candidate = await HubDiscovery.TryEndpointAsync(http, new HubTrust(publicKey), Business,
            SatelliteToken(key, phone.PublicKey), endpoint, 0, TestContext.Current.CancellationToken);

        Assert.Null(candidate);
    }

    // HUB-04: ishonch bulutdan — guvohnoma serverning ochiq kaliti bilan tekshiriladi.
    [Fact]
    public async Task Hub_signed_by_another_server_key_is_not_selected()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var foreignKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKey = PublicKeyOf(key);
        using var hub = await HubTestIdentity.CreateAsync();
        using var phone = await HubTestIdentity.CreateAsync();
        var (server, endpoint) = StartHub(publicKey, HubToken(foreignKey, Business, 5, hub.PublicKey), 5, hub);
        await using var _ = server;
        using var http = HubTestIdentity.Channel(phone);

        var candidate = await HubDiscovery.TryEndpointAsync(http, new HubTrust(publicKey), Business,
            SatelliteToken(key, phone.PublicKey), endpoint, 0, TestContext.Current.CancellationToken);

        Assert.Null(candidate);
    }

    // HUB-04: guvohnomasi yo'q yoki muddati o'tgan tomon ulanmaydi.
    [Fact]
    public async Task Hub_with_an_expired_attestation_is_not_selected()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKey = PublicKeyOf(key);
        using var hub = await HubTestIdentity.CreateAsync();
        using var phone = await HubTestIdentity.CreateAsync();
        var (server, endpoint) = StartHub(publicKey,
            HubToken(key, Business, 5, hub.PublicKey, TimeSpan.FromSeconds(-30)), 5, hub);
        await using var _ = server;
        using var http = HubTestIdentity.Channel(phone);

        var candidate = await HubDiscovery.TryEndpointAsync(http, new HubTrust(publicKey), Business,
            SatelliteToken(key, phone.PublicKey), endpoint, 0, TestContext.Current.CancellationToken);

        Assert.Null(candidate);
    }

    // HUB-05: `epoch` teskari tekshiriladi — vakolatni boy bergan qurilma HUB bo'lib qola olmasin.
    [Fact]
    public async Task Hub_older_than_the_epoch_read_from_the_cloud_is_not_selected()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKey = PublicKeyOf(key);
        using var hub = await HubTestIdentity.CreateAsync();
        using var phone = await HubTestIdentity.CreateAsync();
        var (server, endpoint) = StartHub(publicKey, HubToken(key, Business, 5, hub.PublicKey), 5, hub);
        await using var _ = server;
        using var http = HubTestIdentity.Channel(phone);
        var trust = new HubTrust(publicKey);
        var satellite = SatelliteToken(key, phone.PublicKey);

        Assert.Null(await HubDiscovery.TryEndpointAsync(http, trust, Business,
            satellite, endpoint, 6, TestContext.Current.CancellationToken));
        Assert.Null(await HubDiscovery.TryEndpointAsync(http, trust, Business,
            satellite, endpoint, 12, TestContext.Current.CancellationToken));
        Assert.NotNull(await HubDiscovery.TryEndpointAsync(http, trust, Business,
            satellite, endpoint, 5, TestContext.Current.CancellationToken));
    }

    // HUB-11: bir nechta to'g'ri HUB bo'lsa eng katta `epoch`lisi tanlanadi.
    [Fact]
    public async Task Among_valid_hubs_the_newest_epoch_wins()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKey = PublicKeyOf(key);
        using var older = await HubTestIdentity.CreateAsync();
        using var newer = await HubTestIdentity.CreateAsync();
        using var phone = await HubTestIdentity.CreateAsync();
        var (oldHub, oldEndpoint) = StartHub(publicKey, HubToken(key, Business, 5, older.PublicKey), 5, older);
        await using var _old = oldHub;
        var (newHub, newEndpoint) = StartHub(publicKey, HubToken(key, Business, 8, newer.PublicKey), 8, newer);
        await using var _new = newHub;
        using var http = HubTestIdentity.Channel(phone);
        var trust = new HubTrust(publicKey);
        var satellite = SatelliteToken(key, phone.PublicKey);

        var stale = await HubDiscovery.TryEndpointAsync(http, trust, Business, satellite,
            oldEndpoint, 0, TestContext.Current.CancellationToken);
        var current = await HubDiscovery.TryEndpointAsync(http, trust, Business, satellite,
            newEndpoint, 0, TestContext.Current.CancellationToken);
        Assert.NotNull(stale);
        Assert.NotNull(current);

        var chosen = HubDiscovery.Best([stale, current]);

        Assert.NotNull(chosen);
        Assert.Equal(newEndpoint, chosen.Endpoint);
        Assert.Equal(8, chosen.Attestation.Epoch);
    }

    // HUB-05: eski `epoch`li HUB tanlovga umuman kirmaydi.
    [Fact]
    public async Task A_stale_hub_never_reaches_the_selection()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKey = PublicKeyOf(key);
        using var older = await HubTestIdentity.CreateAsync();
        using var newer = await HubTestIdentity.CreateAsync();
        using var phone = await HubTestIdentity.CreateAsync();
        var (oldHub, oldEndpoint) = StartHub(publicKey, HubToken(key, Business, 5, older.PublicKey), 5, older);
        await using var _old = oldHub;
        var (newHub, newEndpoint) = StartHub(publicKey, HubToken(key, Business, 8, newer.PublicKey), 8, newer);
        await using var _new = newHub;
        using var http = HubTestIdentity.Channel(phone);
        var trust = new HubTrust(publicKey);
        var satellite = SatelliteToken(key, phone.PublicKey);

        var stale = await HubDiscovery.TryEndpointAsync(http, trust, Business, satellite,
            oldEndpoint, 8, TestContext.Current.CancellationToken);
        var current = await HubDiscovery.TryEndpointAsync(http, trust, Business, satellite,
            newEndpoint, 8, TestContext.Current.CancellationToken);

        Assert.Null(stale);
        Assert.NotNull(current);
        Assert.Equal(newEndpoint, HubDiscovery.Best([current])?.Endpoint);
    }

    // HUB-11: tanlov e'londagi raqamga emas, guvohnomadagi `epoch`ga qarab qilinadi.
    [Fact]
    public void Selection_ranks_by_the_attested_epoch_not_the_announced_one()
    {
        var loud = Announced("192.168.1.11", announcedEpoch: 99, attestedEpoch: 2);
        var honest = Announced("192.168.1.12", announcedEpoch: 7, attestedEpoch: 7);

        var chosen = HubDiscovery.Best([loud, honest]);

        Assert.NotNull(chosen);
        Assert.Equal("192.168.1.12", chosen.Endpoint.Host);
        Assert.Equal(7, chosen.Attestation.Epoch);
    }
}
