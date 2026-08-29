using Cartex.Shared.Models.OfflineCache;

namespace Cartex.Hub;

/// HUB-01/HUB-09: HUB o'zida ma'lumot saqlamaydi — hammasi vakolat egasining mavjud
/// oflayn keshi va navbatidan olinadi. Shu interfeys o'sha ikki nuqtani ochadi, xolos.
public interface IHubBackend
{
    HubIdentity Identity { get; }

    /// HUB-09: yo'ldosh uchun katalog — o'sha `OfflineSnapshotDto` shartnomasi (delta bilan).
    Task<OfflineSnapshotDto> CatalogAsync(DateTime? since, CancellationToken cancellationToken);

    /// HUB-06: hodisa HUB navbatiga yoziladi va HUB'ning keyingi ketma-ketligini oladi.
    /// `caller` — guvohnomadan olingan kimlik: amal muallifi shundan yoziladi, tanadan emas.
    Task<HubEventResult> AcceptAsync(
        OfflineSyncEventRequest request, HubAttestationPayload caller, CancellationToken cancellationToken);
}

public sealed record HubIdentity(
    long BusinessId,
    long WarehouseId,
    string WarehouseName,
    long Epoch,
    string DeviceName,
    string AttestationToken,
    string PublicKey);
