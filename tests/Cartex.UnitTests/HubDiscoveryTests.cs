using System.Security.Cryptography;
using Cartex.Hub;
using Cartex.Shared.Models.OfflineCache;
using Xunit;

namespace Cartex.UnitTests;

public class HubDiscoveryTests
{
    private const long Business = 42;
    private const long Warehouse = 7;

    private static string Sign(ECDsa key, long businessId, long epoch, string devicePublicKey, TimeSpan? life = null) =>
        HubAttestation.Sign(key, new HubAttestationPayload
        {
            Role = HubRoles.Hub,
            BusinessId = businessId,
            DeviceId = "hub-device",
            UserId = 5,
            WarehouseId = Warehouse,
            LeaseId = 11,
            Epoch = epoch,
            ExpiresAtUnix = DateTimeOffset.UtcNow.Add(life ?? TimeSpan.FromDays(30)).ToUnixTimeSeconds(),
            DevicePublicKey = devicePublicKey
        });

    private static string Satellite(ECDsa key, string devicePublicKey) =>
        HubAttestation.Sign(key, new HubAttestationPayload
        {
            Role = HubRoles.Satellite,
            BusinessId = Business,
            DeviceId = "phone",
            UserId = 5,
            ExpiresAtUnix = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds(),
            DevicePublicKey = devicePublicKey
        });

    private sealed class FakeBackend(HubIdentity identity) : IHubBackend
    {
        public HubIdentity Identity { get; } = identity;

        public Task<OfflineSnapshotDto> CatalogAsync(DateTime? since, CancellationToken cancellationToken) =>
            Task.FromResult(new OfflineSnapshotDto("UZS", DateTime.UtcNow, [], [], []));

        public Task<HubEventResult> AcceptAsync(
            OfflineSyncEventRequest request, HubAttestationPayload caller, CancellationToken cancellationToken) =>
            Task.FromResult(new HubEventResult(request.EventId, 1, "Queued"));
    }

    private static (HubServer Server, Uri Endpoint) Start(
        string publicKey, string token, long bodyEpoch, HubIdentityKey identity)
    {
        var backend = new FakeBackend(
            new HubIdentity(Business, Warehouse, "Asosiy ombor", bodyEpoch, "Kassa-1", token, publicKey));
        var server = new HubServer(backend, new HubTrust(publicKey), identity.Certificate);
        server.Start(0);
        return (server, new Uri($"https://127.0.0.1:{server.Port}/"));
    }

    private static HubCandidate Candidate(string host, long epoch) =>
        new(new Uri($"https://{host}:{HubDiscovery.Port}/"),
            new HubHelloDto("token", Warehouse, "Asosiy ombor", epoch, "Kassa-1", true),
            new HubAttestationPayload { Role = HubRoles.Hub, BusinessId = Business, LeaseId = 11, Epoch = epoch });

    [Fact]
    public async Task Real_hub_is_found_and_verified()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        using var hubIdentity = await HubTestIdentity.CreateAsync();
        using var phone = await HubTestIdentity.CreateAsync();
        var (server, endpoint) = Start(publicKey, Sign(key, Business, 3, hubIdentity.PublicKey), 3, hubIdentity);
        await using var _ = server;
        using var http = HubTestIdentity.Channel(phone);

        var candidate = await HubDiscovery.TryEndpointAsync(http, new HubTrust(publicKey), Business,
            Satellite(key, phone.PublicKey), endpoint, 0, TestContext.Current.CancellationToken);

        Assert.NotNull(candidate);
        Assert.Equal(endpoint, candidate.Endpoint);
        Assert.Equal("Asosiy ombor", candidate.Hello.WarehouseName);
        Assert.Equal(Warehouse, candidate.Attestation.WarehouseId);
        Assert.Equal(3, candidate.Attestation.Epoch);
        Assert.Equal(11, candidate.Attestation.LeaseId);
        Assert.Equal(hubIdentity.PublicKey, candidate.Attestation.DevicePublicKey);
    }

    [Fact]
    public async Task Hub_of_another_business_is_refused()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        using var hubIdentity = await HubTestIdentity.CreateAsync();
        using var phone = await HubTestIdentity.CreateAsync();
        var (server, endpoint) = Start(publicKey, Sign(key, 999, 3, hubIdentity.PublicKey), 3, hubIdentity);
        await using var _ = server;
        using var http = HubTestIdentity.Channel(phone);

        Assert.Null(await HubDiscovery.TryEndpointAsync(http, new HubTrust(publicKey), Business,
            Satellite(key, phone.PublicKey), endpoint, 0, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Hub_signed_by_another_server_is_refused()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        using var hubIdentity = await HubTestIdentity.CreateAsync();
        using var phone = await HubTestIdentity.CreateAsync();
        var (server, endpoint) = Start(publicKey, Sign(other, Business, 3, hubIdentity.PublicKey), 3, hubIdentity);
        await using var _ = server;
        using var http = HubTestIdentity.Channel(phone);

        Assert.Null(await HubDiscovery.TryEndpointAsync(http, new HubTrust(publicKey), Business,
            Satellite(key, phone.PublicKey), endpoint, 0, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Device_without_the_hub_role_is_refused()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        using var hubIdentity = await HubTestIdentity.CreateAsync();
        using var phone = await HubTestIdentity.CreateAsync();
        var (server, endpoint) = Start(publicKey, Satellite(key, hubIdentity.PublicKey), 0, hubIdentity);
        await using var _ = server;
        using var http = HubTestIdentity.Channel(phone);

        Assert.Null(await HubDiscovery.TryEndpointAsync(http, new HubTrust(publicKey), Business,
            Satellite(key, phone.PublicKey), endpoint, 0, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Expired_hub_attestation_is_refused()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        using var hubIdentity = await HubTestIdentity.CreateAsync();
        using var phone = await HubTestIdentity.CreateAsync();
        var (server, endpoint) = Start(publicKey,
            Sign(key, Business, 3, hubIdentity.PublicKey, TimeSpan.FromMinutes(-1)), 3, hubIdentity);
        await using var _ = server;
        using var http = HubTestIdentity.Channel(phone);

        Assert.Null(await HubDiscovery.TryEndpointAsync(http, new HubTrust(publicKey), Business,
            Satellite(key, phone.PublicKey), endpoint, 0, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Hub_older_than_the_last_known_epoch_is_refused()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        using var hubIdentity = await HubTestIdentity.CreateAsync();
        using var phone = await HubTestIdentity.CreateAsync();
        var (server, endpoint) = Start(publicKey, Sign(key, Business, 3, hubIdentity.PublicKey), 3, hubIdentity);
        await using var _ = server;
        using var http = HubTestIdentity.Channel(phone);
        var trust = new HubTrust(publicKey);
        var satellite = Satellite(key, phone.PublicKey);

        Assert.Null(await HubDiscovery.TryEndpointAsync(http, trust, Business,
            satellite, endpoint, 4, TestContext.Current.CancellationToken));
        Assert.NotNull(await HubDiscovery.TryEndpointAsync(http, trust, Business,
            satellite, endpoint, 3, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Hub_body_that_disagrees_with_its_attestation_is_refused()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        using var hubIdentity = await HubTestIdentity.CreateAsync();
        using var phone = await HubTestIdentity.CreateAsync();
        var (server, endpoint) = Start(publicKey, Sign(key, Business, 3, hubIdentity.PublicKey), 9, hubIdentity);
        await using var _ = server;
        using var http = HubTestIdentity.Channel(phone);

        Assert.Null(await HubDiscovery.TryEndpointAsync(http, new HubTrust(publicKey), Business,
            Satellite(key, phone.PublicKey), endpoint, 0, TestContext.Current.CancellationToken));
    }

    // HUB-04: asosiy hujum — haqiqiy HUB guvohnomasini ushlab olgan, lekin shaxsiy kaliti bo'lmagan
    // qurilma soxta HUB ko'taradi. Yo'ldosh TLS kaliti guvohnomadagi kalitga teng emasligini ko'radi.
    [Fact]
    public async Task Fake_hub_replaying_a_stolen_attestation_is_refused()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        using var realHub = await HubTestIdentity.CreateAsync();
        using var attacker = await HubTestIdentity.CreateAsync();
        using var phone = await HubTestIdentity.CreateAsync();
        var stolen = Sign(key, Business, 3, realHub.PublicKey);
        var (server, endpoint) = Start(publicKey, stolen, 3, attacker);
        await using var _ = server;
        using var http = HubTestIdentity.Channel(phone);

        Assert.Null(await HubDiscovery.TryEndpointAsync(http, new HubTrust(publicKey), Business,
            Satellite(key, phone.PublicKey), endpoint, 0, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Newest_epoch_wins_among_verified_hubs()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        using var staleIdentity = await HubTestIdentity.CreateAsync();
        using var currentIdentity = await HubTestIdentity.CreateAsync();
        using var phone = await HubTestIdentity.CreateAsync();
        var (stale, staleEndpoint) = Start(publicKey, Sign(key, Business, 4, staleIdentity.PublicKey), 4, staleIdentity);
        await using var _s = stale;
        var (current, currentEndpoint) =
            Start(publicKey, Sign(key, Business, 9, currentIdentity.PublicKey), 9, currentIdentity);
        await using var _c = current;
        using var http = HubTestIdentity.Channel(phone);
        var trust = new HubTrust(publicKey);
        var satellite = Satellite(key, phone.PublicKey);

        var first = await HubDiscovery.TryEndpointAsync(http, trust, Business, satellite,
            staleEndpoint, 0, TestContext.Current.CancellationToken);
        var second = await HubDiscovery.TryEndpointAsync(http, trust, Business, satellite,
            currentEndpoint, 0, TestContext.Current.CancellationToken);
        Assert.NotNull(first);
        Assert.NotNull(second);

        var best = HubDiscovery.Best([first, second]);

        Assert.NotNull(best);
        Assert.Equal(currentEndpoint, best.Endpoint);
        Assert.Equal(9, best.Attestation.Epoch);
    }

    [Fact]
    public void Equal_epochs_keep_the_first_responder()
    {
        var best = HubDiscovery.Best([Candidate("192.168.1.5", 6), Candidate("192.168.1.9", 6)]);

        Assert.NotNull(best);
        Assert.Equal("192.168.1.5", best.Endpoint.Host);
        Assert.Null(HubDiscovery.Best([]));
    }

    [Fact]
    public void Hub_qr_round_trips_a_local_endpoint()
    {
        var endpoint = new Uri($"https://192.168.1.5:{HubDiscovery.Port}/");

        var text = HubQr.Format(endpoint);

        Assert.Equal($"cartexhub:https://192.168.1.5:{HubDiscovery.Port}/", text);
        Assert.True(HubQr.TryParse(text, out var parsed));
        Assert.Equal(endpoint, parsed);
    }

    [Theory]
    [InlineData("cartexsrv:https://192.168.1.5:45654/")]
    [InlineData("https://192.168.1.5:45654/")]
    [InlineData("cartexhub:https://8.8.8.8:45654/")]
    // Shifrlanmagan kanal: mijoz ismi, telefoni va qarzi ochiq uchib ketardi.
    [InlineData("cartexhub:http://192.168.1.5:45654/")]
    [InlineData("cartexhub:https://hub.example.com:45654/")]
    [InlineData("cartexhub:https://192.168.1.5@evil.example.com/")]
    [InlineData("cartexhub:")]
    [InlineData("")]
    [InlineData(null)]
    public void Foreign_or_remote_qr_is_refused(string? value)
    {
        Assert.False(HubQr.TryParse(value, out _));
    }
}
