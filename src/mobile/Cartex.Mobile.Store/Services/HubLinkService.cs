using System.Text.Json;
using Cartex.ApiClient.Api;
using Cartex.Hub;
using Cartex.Mobile.Core;
using Cartex.Shared.Models.OfflineCache;

namespace Cartex.Mobile.Store.Services;

// HUB-03: telefon vakolatsiz qurilma sifatida do'kon HUB'iga ulanadi. Bu xizmat faqat
// ishonch va manzilni boshqaradi — savdo mantig'i oflayn rejim bilan bir xil qoladi.
public sealed class HubLinkService(IOfflineCacheApi offlineApi, MobileAuthService auth, HubIdentityService identity)
{
    // Guvohnoma HUB'ga kirish huquqini beradi va butun mijoz bazasini ochadi, ochiq kalit esa
    // ishonchning o'zagi — ikkalasi ham lizing kaliti bilan bir xil himoyada saqlanadi.
    private const string AttestationKey = "hub_attestation_v2";
    // Manzil sir emas va guvohnoma bilan baribir qayta tekshiriladi.
    private const string EndpointKey = "hub_endpoint";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private HubAttestationDto? _attestation;

    public HubHelloDto? Hub { get; private set; }
    public Uri? Endpoint { get; private set; }
    public long HubLeaseId { get; private set; }
    public string? HubPublicKey { get; private set; }
    public bool IsLinked => Hub is not null && Endpoint is not null;

    public bool CanLink => _attestation is { Token.Length: > 0, PublicKey.Length: > 0 };

    // HUB rolini ochish uchun ham shu guvohnoma kerak — ikkala tomon bitta manbadan ishonadi.
    public string? AttestationToken => _attestation?.Token;
    public HubTrust? Trust =>
        _attestation is { PublicKey.Length: > 0 } attestation ? new HubTrust(attestation.PublicKey) : null;
    public long? BusinessId => Credentials()?.BusinessId;
    public string? DeviceId => auth.DeviceId;

    // HUB-12: topish tarmoqdagi 254 manzilga ulanib ko'radi. Mobil internetda operatorning
    // CGNAT tarmog'i ham "lokal" ko'rinadi va bu skanerlash begona abonent qurilmalariga
    // tushardi — shuning uchun qidiruv faqat Wi-Fi/Ethernet da bajariladi.
    public static bool OnLocalNetwork =>
        Connectivity.Current.ConnectionProfiles.Any(x => x is ConnectionProfile.WiFi or ConnectionProfile.Ethernet);

    public async Task LoadAsync()
    {
        if (_attestation is not null) return;
        try
        {
            var json = await SecureStorage.GetAsync(AttestationKey);
            if (!string.IsNullOrWhiteSpace(json))
                _attestation ??= JsonSerializer.Deserialize<HubAttestationDto>(json, Json);
        }
        catch
        {
            // Kalit ombori ochilmasa guvohnoma yo'q deb hisoblanadi: keyingi onlayn sikl uni
            // qayta yozadi, HUB esa guvohnomasiz baribir ishlamaydi.
        }
    }

    // HUB-04: guvohnoma onlayn paytda yangilanadi — oflayn qolganda uni olishning iloji yo'q.
    // Qurilmaning ochiq kaliti so'rov bilan ketadi: guvohnoma o'sha kalitga bog'lanadi va uni
    // ushlab olgan begona qurilma TLS'da kalitni ko'rsata olmaydi.
    public async Task RefreshAttestationAsync()
    {
        if (auth.UserId is null) return;
        try
        {
            var key = await identity.KeyAsync();
            var attestation = await offlineApi.GetHubAttestationAsync(new HubAttestationRequest(key.PublicKey));
            // HUB-05: `epoch` faqat oldinga siljiydi. Kechikkan yoki qayta o'ynatilgan javob uni
            // orqaga tortsa, vakolatini yo'qotgan eski HUB yana qabul qilinadigan bo'lardi.
            _attestation = attestation with { Epoch = Math.Max(attestation.Epoch, _attestation?.Epoch ?? 0) };
            await SecureStorage.SetAsync(AttestationKey, JsonSerializer.Serialize(_attestation, Json));
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException
                                               or JsonException or Refit.ApiException)
        {
            System.Diagnostics.Debug.WriteLine(exception);
        }
    }

    // Bulut qaytganda ulanish uziladi, lekin guvohnoma saqlanadi — u 30 kun amal qiladi va
    // keyingi uzilishda qayta olishning iloji bo'lmasligi mumkin.
    public void Unlink()
    {
        Hub = null;
        Endpoint = null;
        HubLeaseId = 0;
        HubPublicKey = null;
    }

    // Chiqish yoki server almashish: guvohnoma do'konga bog'langan, boshqa do'konda ishlamasligi shart.
    public void Forget()
    {
        SecureStorage.Remove(AttestationKey);
        Preferences.Remove(EndpointKey);
        _attestation = null;
        Unlink();
    }

    // HUB-11: uchala kanal ham `HubDiscovery` da — saqlangan manzil, tarmoqdagi e'lon va
    // tarmoqni faol tekshirish.
    public async Task<bool> TryLinkAsync(CancellationToken cancellationToken = default)
    {
        if (!OnLocalNetwork || Credentials() is not { } credentials) return false;

        var saved = SavedEndpoint();
        var found = await HubDiscovery.FindAsync(await identity.ChannelAsync(cancellationToken),
            credentials.Trust, credentials.BusinessId, credentials.Token, saved,
            _attestation?.Epoch ?? 0, cancellationToken);
        if (found is null)
        {
            // Manzil DHCP bilan almashadi: javob bermagani saqlanib qolsa keyingi qidiruv ham
            // o'sha o'lik manzilni kutishdan boshlanadi.
            if (saved is not null) Preferences.Remove(EndpointKey);
            return false;
        }

        Adopt(found);
        return true;
    }

    // HUB-11: qo'lda ulash (QR). Manzil foydalanuvchidan keladi, ishonch esa baribir shu yerda —
    // guvohnomasi tekshirilmagan xost qabul qilinmaydi.
    public async Task<bool> LinkToAsync(Uri endpoint, CancellationToken cancellationToken = default)
    {
        if (!OnLocalNetwork || Credentials() is not { } credentials) return false;

        var found = await HubDiscovery.TryEndpointAsync(await identity.ChannelAsync(cancellationToken),
            credentials.Trust, credentials.BusinessId, credentials.Token, endpoint,
            _attestation?.Epoch ?? 0, cancellationToken);
        if (found is null) return false;

        Adopt(found);
        return true;
    }

    public async Task<OfflineSnapshotDto?> CatalogAsync(CancellationToken cancellationToken)
    {
        if (Endpoint is null || await ClientAsync(cancellationToken) is not { } client) return null;
        return await client.CatalogAsync(Endpoint, null, cancellationToken);
    }

    public async Task<HubEventResult?> SendAsync(
        OfflineSyncEventRequest request, CancellationToken cancellationToken)
    {
        if (Endpoint is null || await ClientAsync(cancellationToken) is not { } client) return null;
        return await client.SendAsync(Endpoint, request, cancellationToken);
    }

    private void Adopt(HubCandidate candidate)
    {
        Hub = candidate.Hello;
        Endpoint = candidate.Endpoint;
        HubLeaseId = candidate.Attestation.LeaseId;
        HubPublicKey = candidate.Attestation.DevicePublicKey;
        Preferences.Set(EndpointKey, candidate.Endpoint.ToString());
    }

    private static Uri? SavedEndpoint() =>
        Preferences.Get(EndpointKey, null) is { Length: > 0 } value
        && Uri.TryCreate(value, UriKind.Absolute, out var endpoint)
            ? endpoint
            : null;

    private (HubTrust Trust, long BusinessId, string Token)? Credentials()
    {
        if (_attestation is not { Token.Length: > 0, PublicKey.Length: > 0 } attestation) return null;
        return HubAttestation.Verify(attestation.Token, attestation.PublicKey, DateTime.UtcNow) is { } payload
            ? (new HubTrust(attestation.PublicKey), payload.BusinessId, attestation.Token)
            : null;
    }

    private async Task<HubClient?> ClientAsync(CancellationToken cancellationToken) =>
        Credentials() is { } credentials && HubPublicKey is { Length: > 0 } hub
            ? new HubClient(await identity.ChannelAsync(cancellationToken), credentials.Token, hub)
            : null;
}
