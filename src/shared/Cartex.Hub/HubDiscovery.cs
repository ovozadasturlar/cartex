using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Cartex.Shared.Models.OfflineCache;

namespace Cartex.Hub;

public sealed record HubCandidate(Uri Endpoint, HubHelloDto Hello, HubAttestationPayload Attestation);

/// HUB-11: yo'ldosh HUB'ni uch kanal bilan qidiradi — saqlangan manzil, tarmoqdagi e'lon va
/// tarmoqni faol tekshirish. Uchinchisi kerak, chunki ko'p router "client isolation" yoki
/// broadcast filtri bilan e'lonni yo'q qiladi va e'longa tayangan topish umuman ishlamaydi.
/// Kanallar faqat manzil beradi; ishonch yagona nuqtada — imzolangan guvohnomada (`Accept`).
public static class HubDiscovery
{
    public const int Port = HubServer.DefaultPort;

    private const int MaxParallelProbes = 64;
    private const long MaxHelloBytes = 64 * 1024;
    // E'lon oralig'idan (10 s) uzunroq: oyna qisqa bo'lsa eng yangi HUB e'loni umuman eshitilmasdi.
    private static readonly TimeSpan BeaconWindow = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan HelloTimeout = TimeSpan.FromSeconds(3);

    public static async Task<HubCandidate?> FindAsync(
        HttpClient http, HubTrust trust, long businessId, string attestationToken,
        Uri? savedEndpoint, long minEpoch, CancellationToken cancellationToken)
    {
        // Saqlangan manzil o'z chegarasi bilan sinaladi: DHCP bilan manzil ko'chgan bo'lsa, qolgan
        // kanallar klientning to'liq kutish vaqtini o'lik xostda o'tkazib yubormasin.
        if (savedEndpoint is not null)
        {
            using var known = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            known.CancelAfter(HelloTimeout);
            if (await TryEndpointAsync(http, trust, businessId, attestationToken, savedEndpoint, minEpoch, known.Token)
                is { } saved)
                return saved;
        }

        if (await ListenAsync(http, trust, businessId, attestationToken, BeaconWindow, minEpoch, cancellationToken)
            is { } heard)
            return heard;

        return Best(await ProbeSubnetAsync(http, trust, businessId, attestationToken, minEpoch, cancellationToken));
    }

    public static async Task<HubCandidate?> TryEndpointAsync(
        HttpClient http, HubTrust trust, long businessId, string attestationToken,
        Uri endpoint, long minEpoch, CancellationToken cancellationToken)
    {
        if (!endpoint.IsAbsoluteUri) return null;

        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, new Uri(endpoint, "hub/hello"));
            message.Headers.Add(HubHeaders.Attestation, attestationToken);
            using var response = await http.SendAsync(message, cancellationToken);

            // Tarmoqda begona xost ham javob berishi mumkin; haqiqiy salom yarim kilobayt bo'ladi,
            // shuning uchun uzunligi noma'lum yoki katta tana o'qilmasdan tashlanadi.
            if (!response.IsSuccessStatusCode
                || response.Content.Headers.ContentLength is null or > MaxHelloBytes)
                return null;

            var hello = await response.Content.ReadFromJsonAsync<HubHelloDto>(HubJson.Options, cancellationToken);
            return Accept(trust, businessId, endpoint, hello, minEpoch,
                message.Options.TryGetValue(HubTransport.PeerKey, out var peer) ? peer : null);
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException
                                              or JsonException or NotSupportedException
                                              or FormatException or InvalidOperationException)
        {
            return null;
        }
    }

    public static async Task<HubCandidate?> ListenAsync(
        HttpClient http, HubTrust trust, long businessId, string attestationToken,
        TimeSpan window, long minEpoch, CancellationToken cancellationToken)
    {
        try
        {
            // Bandlik yoki ruxsat tufayli tinglash ochilmasligi mumkin — bu topishning oxiri emas,
            // qolgan kanallar baribir sinaladi.
            if (await HubBeacon.ListenAsync(window, cancellationToken, businessId) is not { } heard)
                return null;

            return await TryEndpointAsync(
                http, trust, businessId, attestationToken, heard.Endpoint, minEpoch, cancellationToken);
        }
        catch (Exception exception) when (exception is SocketException or OperationCanceledException)
        {
            return null;
        }
    }

    /// Javob bergan tartibda qaytadi — `Best` teng `epoch`da birinchisini tanlaydi.
    public static async Task<IReadOnlyList<HubCandidate>> ProbeSubnetAsync(
        HttpClient http, HubTrust trust, long businessId, string attestationToken,
        long minEpoch, CancellationToken cancellationToken)
    {
        // HUB-12: faol tekshirish faqat do'kon tarmog'ida — ommaviy manzillar skanerlanmaydi.
        if (HubNetwork.LocalAddress() is not { } local
            || local.AddressFamily != AddressFamily.InterNetwork
            || !HubNetwork.IsLocal(local))
            return [];

        var bytes = local.GetAddressBytes();
        var found = new List<HubCandidate>();
        using var gate = new SemaphoreSlim(MaxParallelProbes);

        async Task ProbeAsync(IPAddress address)
        {
            try
            {
                await gate.WaitAsync(cancellationToken);
                try
                {
                    using var socket = new TcpClient();
                    using var connect = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    connect.CancelAfter(ConnectTimeout);
                    await socket.ConnectAsync(address, Port, connect.Token);
                }
                finally
                {
                    gate.Release();
                }
            }
            catch (Exception exception) when (exception is SocketException or OperationCanceledException
                                                  or ObjectDisposedException)
            {
                return;
            }

            using var hello = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            hello.CancelAfter(HelloTimeout);
            if (await TryEndpointAsync(http, trust, businessId, attestationToken,
                    new Uri($"https://{address}:{Port}/"), minEpoch, hello.Token) is not { } candidate)
                return;

            lock (found)
            {
                found.Add(candidate);
            }
        }

        var probes = new List<Task>();
        for (var host = 1; host < 255; host++)
        {
            if (host == bytes[3]) continue;
            probes.Add(ProbeAsync(new IPAddress([bytes[0], bytes[1], bytes[2], (byte)host])));
        }

        try
        {
            await Task.WhenAll(probes);
        }
        catch (Exception exception) when (exception is SocketException or OperationCanceledException)
        {
        }

        lock (found)
        {
            return [.. found];
        }
    }

    /// Konflikt: bir nechta HUB javob bersa eng yangi vakolat yutadi. Tarmoqdan kelgan ma'lumot
    /// emas, faqat imzolangan guvohnomadagi `epoch` solishtiriladi.
    public static HubCandidate? Best(IReadOnlyList<HubCandidate> candidates) =>
        candidates.MaxBy(candidate => candidate.Attestation.Epoch);

    /// HUB-05: guvohnoma haqiqiy, muddati o'tmagan, shu do'konniki, roli HUB va `epoch` bulutdan
    /// o'qilgan oxirgisidan eski emas. Tana guvohnomaga mos bo'lishi ham shart — aks holda HUB
    /// o'zini yangiroq ko'rsatib, tanlovni chalg'itardi. Eng oxirgi shart — TLS'da ko'rsatilgan
    /// kalit guvohnomadagi kalit bo'lishi: shusiz haqiqiy guvohnomani ushlab olgan soxta HUB
    /// butun smena savdosini o'ziga yig'ib olardi. O'tmagan javob jim tashlanadi.
    private static HubCandidate? Accept(
        HubTrust trust, long businessId, Uri endpoint, HubHelloDto? hello, long minEpoch, string? peerPublicKey) =>
        hello is not null
        && trust.VerifyHub(hello.Token, businessId, minEpoch: minEpoch) is { } payload
        && payload.Epoch == hello.Epoch
        && HubIdentityKey.SameKey(peerPublicKey, payload.DevicePublicKey)
            ? new HubCandidate(endpoint, hello, payload)
            : null;
}

/// Qo'lda ulash: HUB manzili QR orqali beriladi. Bulut serverining `cartexsrv:` prefiksidan
/// alohida — u boshqa protokol va boshqa ishonch, ikkalasini aralashtirib bo'lmaydi.
public static class HubQr
{
    public const string Prefix = "cartexhub:";

    public static string Format(Uri endpoint) => Prefix + endpoint.AbsoluteUri;

    /// Faqat lokal IPv4 va faqat `https`: domen nomini oflaynda tekshirib bo'lmaydi, skanerlangan QR
    /// esa qurilmani do'kondan tashqaridagi xostga yoki shifrlanmagan kanalga yo'naltirmasligi shart.
    public static bool TryParse(string? value, out Uri endpoint)
    {
        endpoint = null!;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var trimmed = value.Trim();
        if (!trimmed.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
            || !Uri.TryCreate(trimmed[Prefix.Length..].Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !IPAddress.TryParse(uri.Host, out var address)
            || address.AddressFamily != AddressFamily.InterNetwork
            || !HubNetwork.IsLocal(address))
            return false;

        endpoint = uri;
        return true;
    }
}
