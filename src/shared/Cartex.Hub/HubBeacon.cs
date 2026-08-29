using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Cartex.Shared.Models.OfflineCache;

namespace Cartex.Hub;

/// HUB-11: HUB o'zini tarmoqqa e'lon qilib turadi. Manzil qo'lda kiritilmasin degan yagona
/// maqsad — e'lon ishonch bermaydi va guvohnoma olib yurmaydi; ishonch TLS ichida tekshiriladi.
public sealed class HubBeacon : IAsyncDisposable
{
    public const int Port = 45655;

    /// E'lon shifrlanmagan broadcast: u qanchalik kam yuborilsa, do'kon tarkibi haqidagi ma'lumot
    /// ham shunchalik kam tarqaladi. Topish uchun 10 soniya yetarli.
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    private CancellationTokenSource? _cts;
    private Task? _loop;

    public bool IsRunning => _loop is { IsCompleted: false };

    public void StartAnnouncing(Func<HubBeaconDto?> snapshot)
    {
        if (IsRunning) return;
        _cts = new CancellationTokenSource();
        _loop = AnnounceAsync(snapshot, _cts.Token);
    }

    public async Task StopAsync()
    {
        if (_cts is null) return;
        await _cts.CancelAsync();
        if (_loop is not null)
            try { await _loop; } catch (OperationCanceledException) { }
        _cts.Dispose();
        _cts = null;
        _loop = null;
    }

    public async ValueTask DisposeAsync() => await StopAsync();

    private static async Task AnnounceAsync(Func<HubBeaconDto?> snapshot, CancellationToken cancellationToken)
    {
        using var socket = new UdpClient { EnableBroadcast = true };
        while (!cancellationToken.IsCancellationRequested)
        {
            // Har manzil alohida: Androidda `255.255.255.255` ga yuborish ko'pincha yiqiladi va
            // umumiy `catch` bo'lsa qolgan manzillar (tarmoqning o'z broadcast'i) sinalmay qolardi.
            // E'lon yordamchi qulaylik — uning xatosi HUB xizmatini to'xtatmaydi.
            foreach (var target in Targets(snapshot))
            {
                try
                {
                    await socket.SendAsync(target.Payload, new IPEndPoint(target.Address, Port), cancellationToken);
                }
                catch (Exception exception) when (exception is SocketException or ObjectDisposedException)
                {
                }
            }

            try
            {
                await Task.Delay(Interval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// Ba'zi Wi-Fi drayverlari 255.255.255.255 ni filtrlaydi, shuning uchun tarmoqning
    /// o'z broadcast manzili ham qo'shiladi. Androidda `IPv4Mask` ko'pincha berilmaydi —
    /// bunday holatda do'kon tarmog'i uchun odatiy /24 qabul qilinadi, aks holda e'lon
    /// faqat filtrlanadigan manzilga ketib, qurilmalar bir-birini topa olmasdi.
    private static IEnumerable<(IPAddress Address, byte[] Payload)> Targets(Func<HubBeaconDto?> snapshot)
    {
        List<IPAddress> targets;
        byte[] payload;
        try
        {
            if (snapshot() is not { } beacon) yield break;
            payload = JsonSerializer.SerializeToUtf8Bytes(beacon, HubJson.Options);
            targets = [.. BroadcastTargets()];
        }
        catch (Exception exception) when (exception is NetworkInformationException or PlatformNotSupportedException)
        {
            yield break;
        }

        foreach (var target in targets)
            yield return (target, payload);
    }

    private static IEnumerable<IPAddress> BroadcastTargets()
    {
        yield return IPAddress.Broadcast;
        var seen = new HashSet<string>();
        if (HubNetwork.LocalAddress() is { } local)
        {
            var bytes = local.GetAddressBytes();
            bytes[3] = 255;
            var subnet = new IPAddress(bytes);
            seen.Add(subnet.ToString());
            yield return subnet;
        }

        foreach (var adapter in HubNetwork.Adapters())
        {
            if (adapter.OperationalStatus != OperationalStatus.Up
                || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;

            foreach (var info in adapter.GetIPProperties().UnicastAddresses)
            {
                if (info.Address.AddressFamily != AddressFamily.InterNetwork
                    || !HubNetwork.IsLocal(info.Address))
                    continue;

                var address = info.Address.GetAddressBytes();
                var mask = info.IPv4Mask?.GetAddressBytes() ?? [255, 255, 255, 0];
                for (var i = 0; i < address.Length; i++)
                    address[i] = (byte)(address[i] | ~mask[i]);
                var target = new IPAddress(address);
                if (seen.Add(target.ToString()))
                    yield return target;
            }
        }
    }

    /// `businessId` berilsa faqat o'sha do'konning e'loni qabul qilinadi — bitta Wi-Fi'da ikki
    /// do'kon ishlayotgan bo'lsa, birinchi eshitilgan e'lon begonaniki bo'lishi mumkin.
    /// Oyna oxirigacha tinglanadi va eng katta `epoch`li e'lon qaytariladi: birinchi javobni olish
    /// hujjatsiz raqib uchun oddiy poygaga aylanardi — u faqat tezroq gapirsa yetardi.
    public static async Task<(HubBeaconDto Beacon, Uri Endpoint)?> ListenAsync(
        TimeSpan timeout, CancellationToken cancellationToken, long? businessId = null)
    {
        using var socket = new UdpClient();
        socket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        socket.Client.Bind(new IPEndPoint(IPAddress.Any, Port));
        using var window = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        window.CancelAfter(timeout);

        (HubBeaconDto Beacon, Uri Endpoint)? best = null;
        try
        {
            while (!window.IsCancellationRequested)
            {
                var result = await socket.ReceiveAsync(window.Token);
                if (!HubNetwork.IsLocal(result.RemoteEndPoint.Address)) continue;
                if (Read(result.Buffer) is not { } beacon
                    || beacon.Port is <= 0 or >= 65536
                    || (businessId is not null && beacon.BusinessId != businessId)
                    || (best is not null && beacon.Epoch <= best.Value.Beacon.Epoch))
                    continue;
                best = (beacon, new Uri($"https://{result.RemoteEndPoint.Address}:{beacon.Port}/"));
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException or SocketException)
        {
        }

        return best;
    }

    /// Buzuq paket tinglashni to'xtatmaydi: aks holda bitta axlat datagramma butun topishni
    /// o'chirib qo'yish uchun yetardi.
    private static HubBeaconDto? Read(byte[] payload)
    {
        try
        {
            return JsonSerializer.Deserialize<HubBeaconDto>(Encoding.UTF8.GetString(payload), HubJson.Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
