using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cartex.ApiClient.Api;
using Cartex.Hub;
using Cartex.Shared.Models.OfflineCache;

namespace Cartex.UI.Services;

/// HUB-03: vakolati yo'q kompyuter ham do'kon HUB'iga ulanib ishlashda davom etadi.
/// Ishonch va manzil shu yerda; savdo mantig'i oflayn rejim bilan bir xil qoladi.
public sealed class HubClientService(
    IOfflineCacheApi offlineApi, HubIdentityService identity, HubCredentialStore credentials)
{
    // Qo'lda kiritilgan manzil topish chegarasida sinaladi: umumiy klient kutish vaqti katalog
    // uchun o'lchangan, o'lik manzilda esa foydalanuvchi 15 soniya javobsiz qolardi.
    private static readonly TimeSpan DiscoveryTimeout = TimeSpan.FromSeconds(3);

    public HubHelloDto? Hub { get; private set; }
    public Uri? Endpoint { get; private set; }
    public long HubLeaseId { get; private set; }
    public string? HubPublicKey { get; private set; }
    public bool IsLinked => Hub is not null && Endpoint is not null;

    public HubCredential? Credential => credentials.Load();

    public bool CanLink => Context() is not null;

    public void Unlink()
    {
        Hub = null;
        Endpoint = null;
        HubPublicKey = null;
    }

    /// HUB-11: uch kanal — saqlangan manzil, tarmoqdagi e'lon va tarmoqni faol tekshirish.
    public async Task<bool> TryLinkAsync(CancellationToken cancellationToken = default)
    {
        if (Context() is not { } context) return false;

        var http = await identity.ChannelAsync(cancellationToken);
        var found = await HubDiscovery.FindAsync(http, context.Trust, context.BusinessId, context.Token,
            context.Endpoint, context.MinEpoch, cancellationToken);
        if (found is null)
        {
            // Saqlangan manzil ham javob bermadi: keyingi urinish uni kutib o'tirmasin.
            if (context.Endpoint is not null) Remember(null);
            return false;
        }

        if (context.Endpoint == found.Endpoint)
            found = await NewerAsync(http, context, found.Attestation.Epoch, cancellationToken) ?? found;
        Apply(found);
        return true;
    }

    /// Qo'lda ulash: `cartexhub:` QR matni yoki manzilning o'zi. Manzil baribir guvohnoma
    /// bilan tekshiriladi — kiritilgan qiymat faqat qayerga qarashni ko'rsatadi.
    public async Task<bool> TryLinkManualAsync(string? value, CancellationToken cancellationToken = default)
    {
        if (Context() is not { } context || !TryEndpoint(value, out var endpoint)) return false;

        var http = await identity.ChannelAsync(cancellationToken);
        using var probe = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        probe.CancelAfter(DiscoveryTimeout);
        var found = await HubDiscovery.TryEndpointAsync(http, context.Trust, context.BusinessId, context.Token,
            endpoint, context.MinEpoch, probe.Token);
        if (found is null) return false;
        Apply(found);
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

    /// HUB-04: guvohnoma faqat onlayn paytda olinadi; oflayn qolgandan keyin kech bo'ladi.
    /// Qurilmaning ochiq kaliti so'rov bilan ketadi — guvohnoma o'sha kalitga bog'lanadi,
    /// javobda esa joriy vakolat `epoch`i keladi, ya'ni ikkinchi so'rov kerak emas.
    public async Task RefreshAttestationAsync()
    {
        try
        {
            var key = await identity.KeyAsync();
            var attestation = await offlineApi.GetHubAttestationAsync(new HubAttestationRequest(key.PublicKey));
            credentials.Save(new HubCredential(
                attestation.Token, attestation.PublicKey, attestation.Epoch, credentials.Load()?.Endpoint));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
        }
    }

    /// HUB-11: saqlangan manzil zinapoyani chetlab o'tadi — u birinchi javob berganni oladi.
    /// Vakolat boshqa qurilmaga ko'chganda eski rol egasi tarmoqda hali javob berayotgan bo'ladi,
    /// shuning uchun tarmoq bir marta so'raladi va faqat **qat'iy kattaroq** `epoch` uni almashtiradi
    /// (teng bo'lsa saqlangan manzil qoladi). E'lon emas, faol tekshirish: e'lonni ko'p router
    /// bloklaydi va uning oynasi baribir uzunroq.
    private static async Task<HubCandidate?> NewerAsync(
        HttpClient http, HubContext context, long epoch, CancellationToken cancellationToken) =>
        HubDiscovery.Best(await HubDiscovery.ProbeSubnetAsync(
            http, context.Trust, context.BusinessId, context.Token, epoch + 1, cancellationToken));

    private void Apply(HubCandidate candidate)
    {
        Hub = candidate.Hello;
        Endpoint = candidate.Endpoint;
        HubLeaseId = candidate.Attestation.LeaseId;
        HubPublicKey = candidate.Attestation.DevicePublicKey;
        Remember(candidate.Endpoint.ToString());
    }

    // Guvohnoma shu orada yangilangan bo'lishi mumkin — faqat manzil ustiga yoziladi.
    private void Remember(string? endpoint)
    {
        if (credentials.Load() is { } stored) credentials.Save(stored with { Endpoint = endpoint });
    }

    private static bool TryEndpoint(string? value, out Uri endpoint)
    {
        endpoint = null!;
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text)) return false;
        if (HubQr.TryParse(text, out endpoint)) return true;

        if (!text.Contains("://", StringComparison.Ordinal)) text = "https://" + text;
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)) return false;
        if (uri.IsDefaultPort) uri = new UriBuilder(uri) { Port = HubDiscovery.Port }.Uri;
        // Tekshiruv yagona joyda: qo'lda kiritilgan manzil ham QR bilan bir xil chegarada qoladi.
        return HubQr.TryParse(HubQr.Format(uri), out endpoint);
    }

    private async Task<HubClient?> ClientAsync(CancellationToken cancellationToken) =>
        Context() is { } context && HubPublicKey is { Length: > 0 } hub
            ? new HubClient(await identity.ChannelAsync(cancellationToken), context.Token, hub)
            : null;

    /// HUB-05: bulutda faol vakolat bo'lmasa (`epoch = 0`) hech bir HUB qabul qilinmaydi —
    /// vakolatini boy bergan qurilmaning guvohnomasi yana 30 kun haqiqiy ko'rinadi.
    private HubContext? Context()
    {
        if (credentials.Load() is not { AuthorityEpoch: > 0 } stored) return null;
        return HubAttestation.Verify(stored.Token, stored.ServerKey, DateTime.UtcNow) is { } payload
            ? new HubContext(new HubTrust(stored.ServerKey), payload.BusinessId, stored.Token,
                stored.AuthorityEpoch,
                Uri.TryCreate(stored.Endpoint, UriKind.Absolute, out var endpoint) ? endpoint : null)
            : null;
    }

    private sealed record HubContext(HubTrust Trust, long BusinessId, string Token, long MinEpoch, Uri? Endpoint);
}
