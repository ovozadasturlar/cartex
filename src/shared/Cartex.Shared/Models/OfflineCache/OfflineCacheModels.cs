using System.Text.Json;
using Cartex.Shared.Models.Stocks;

namespace Cartex.Shared.Models.OfflineCache;

public sealed record OfflineCacheStateDto(
    string? DeviceId,
    string? DeviceName,
    DateTime? ClaimedAt,
    long? LeaseId = null,
    long? WarehouseId = null,
    string? WarehouseName = null,
    long Epoch = 0,
    DateTime? LastHeartbeatAt = null,
    DateTime? LastSyncAt = null,
    long LastAcceptedSequence = 0,
    long LastReportedPendingCount = 0,
    bool IsCurrentDevice = false,
    bool IsHolderPossiblyOffline = false);

public sealed record ClaimOfflineCacheRequest(
    string DeviceId,
    string DeviceName,
    long WarehouseId = 0);

public sealed record OfflineLeaseGrantDto(
    long LeaseId,
    long WarehouseId,
    long Epoch,
    string LeaseToken,
    DateTime ClaimedAt,
    DateTime LastHeartbeatAt,
    long LastAcceptedSequence);

public sealed record ReleaseOfflineCacheRequest(
    long? LeaseId = null,
    string? LeaseToken = null,
    bool Force = false,
    string? Reason = null);

public sealed record OfflineHeartbeatRequest(long LeaseId, long Epoch, string LeaseToken, long PendingCount = 0);
public sealed record OfflineHeartbeatDto(DateTime ServerTime, long LastAcceptedSequence, bool IsActive);

public sealed record OfflineBarcodeDto(long VariantId, string Code, decimal PackQty);

public sealed record OfflineCustomerDto(
    long Id,
    string FullName,
    string? Phone,
    string? CardBarcode,
    decimal DiscountPct,
    decimal DebtBalance,
    decimal? CreditLimit);

public sealed record OfflineParticipantRoleDto(
    long Id,
    string Key,
    string SingularLabel,
    bool IsRequired,
    bool CanEqualBuyer,
    int MaxCount,
    int SortOrder);

public sealed record OfflinePartnerDto(
    long PartnerId,
    long PartyId,
    string PartnerCode,
    string FullName,
    string? Phone,
    long? CustomerId);

public sealed record OfflineSnapshotDto(
    string BaseCurrency,
    DateTime ServerTime,
    List<StockOnHandDto> Products,
    List<OfflineBarcodeDto> Barcodes,
    List<OfflineCustomerDto> Customers,
    bool AllowDebtSales = true,
    bool AllowInsufficientStockSales = false,
    long LeaseId = 0,
    long Epoch = 0,
    long SnapshotVersion = 0,
    List<OfflineParticipantRoleDto>? ParticipantRoles = null,
    List<OfflinePartnerDto>? Partners = null);

public sealed record OfflineSyncEventRequest(
    Guid EventId,
    long Sequence,
    string Kind,
    string IdempotencyKey,
    DateTime OccurredAt,
    JsonElement Payload,
    long? ActorUserId = null);

public sealed record OfflineSyncBatchRequest(
    long LeaseId,
    long Epoch,
    string LeaseToken,
    IReadOnlyList<OfflineSyncEventRequest> Events);

public sealed record OfflineSyncEventResult(
    Guid EventId,
    long Sequence,
    string Status,
    long? ResultEntityId = null,
    string? ResultCode = null,
    string? ErrorCode = null,
    string? Error = null);

public sealed record OfflineSyncBatchResult(
    long LeaseId,
    long Epoch,
    long LastAcceptedSequence,
    DateTime ServerTime,
    IReadOnlyList<OfflineSyncEventResult> Results);
