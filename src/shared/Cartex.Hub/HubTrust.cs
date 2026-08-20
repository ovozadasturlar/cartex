using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Cartex.Shared.Models.OfflineCache;

namespace Cartex.Hub;

/// HUB-04: ikkala tomon ham bir-birini bulut imzosi bilan tekshiradi. Ochiq kalit onlayn
/// paytda olinadi va shu yerda turadi — tarmoqdan kelgan kalitga hech qachon ishonilmaydi.
public sealed class HubTrust(string publicKey)
{
    public string PublicKey { get; } = publicKey;

    public HubAttestationPayload? Verify(string? token, long businessId) =>
        HubAttestation.Verify(token, PublicKey, DateTime.UtcNow) is { } payload
        && payload.BusinessId == businessId
            ? payload
            : null;

    /// Yo'ldosh tomoni: HUB haqiqatan shu do'konning vakolat egasimi. `minEpoch` — bulutdan
    /// o'qilgan oxirgi epoch: guvohnoma 30 kun yashagani uchun vakolatni boy bergan qurilma ham
    /// o'zini HUB deb ko'rsatib, savdolarni bulutga bormaydigan navbatga yig'ib qo'yishi mumkin.
    public HubAttestationPayload? VerifyHub(
        string? token, long businessId, long? warehouseId = null, long? minEpoch = null) =>
        Verify(token, businessId) is { Role: HubRoles.Hub } payload
        && payload.LeaseId > 0
        && (warehouseId is null || payload.WarehouseId == warehouseId)
        && (minEpoch is null || payload.Epoch >= minEpoch)
            ? payload
            : null;
}

public static class HubNetwork
{
    /// Androidda bu ro'yxat SELinux tufayli istisno tashlashi mumkin — chaqiruvchi qulab
    /// tushmasligi uchun bo'sh ro'yxat qaytariladi.
    public static IReadOnlyList<NetworkInterface> Adapters()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces();
        }
        catch (Exception exception) when (exception is NetworkInformationException or PlatformNotSupportedException)
        {
            return [];
        }
    }

    public static bool IsLocal(IPAddress address)
    {
        if (IPAddress.IsLoopback(address)) return true;
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal;
        if (address.AddressFamily != AddressFamily.InterNetwork) return false;

        var bytes = address.GetAddressBytes();
        return bytes[0] switch
        {
            10 => true,
            127 => true,
            169 => bytes[1] == 254,
            172 => bytes[1] >= 16 && bytes[1] <= 31,
            192 => bytes[1] == 168,
            _ => false
        };
    }

    /// E'lon va QR uchun qurilmaning tarmoqdagi manzili. Androidda interfeys ro'yxati bo'sh
    /// qaytishi mumkin — bunday holatda marshrut jadvalidan aniqlanadi (UDP `Connect` paket
    /// yubormaydi, faqat lokal manzilni tanlaydi).
    /// Marshrut birinchi: Android SELinux `GetAllNetworkInterfaces()` uchun kerakli socket
    /// ioctl'ini bloklaydi va u istisno tashlaydi — bu yo'l esa hamma joyda ishlaydi.
    public static IPAddress? LocalAddress() => FromRoute() ?? FromInterfaces();

    [SuppressMessage("Meziantou.Analyzer", "MA0045",
        Justification = "UDP `Connect` tarmoqqa chiqmaydi — faqat marshrut bo'yicha lokal manzilni tanlaydi.")]
    private static IPAddress? FromRoute()
    {
        try
        {
            using var probe = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            probe.Connect(new IPEndPoint(IPAddress.Parse("8.8.8.8"), 9));
            return probe.LocalEndPoint is IPEndPoint local && IsLocal(local.Address) ? local.Address : null;
        }
        catch (SocketException)
        {
            return null;
        }
    }

    private static IPAddress? FromInterfaces()
    {
        foreach (var adapter in Adapters())
        {
            if (adapter.OperationalStatus != OperationalStatus.Up
                || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;

            foreach (var info in adapter.GetIPProperties().UnicastAddresses)
            {
                if (info.Address.AddressFamily != AddressFamily.InterNetwork || !IsLocal(info.Address))
                    continue;
                var bytes = info.Address.GetAddressBytes();
                if (bytes[0] != 169 || bytes[1] != 254)
                    return info.Address;
            }
        }
        return null;
    }
}
