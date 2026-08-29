using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public sealed class OfflineAuthorityLease : AuditableEntity
{
    public long BusinessId { get; set; }
    public Business Business { get; set; } = null!;
    public long BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public long WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    public string DeviceId { get; set; } = null!;
    public string DeviceName { get; set; } = null!;
    public string TokenHash { get; set; } = null!;
    public long Epoch { get; set; }
    public long LastAcceptedSequence { get; set; }
    public long LastReportedPendingCount { get; set; }
    public DateTime ClaimedAt { get; set; }
    public DateTime LastHeartbeatAt { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public long? RevokedByUserId { get; set; }
    public User? RevokedByUser { get; set; }
    public string? RevokeReason { get; set; }
    public int Version { get; set; } = 1;

    public ICollection<OfflineSyncEvent> Events { get; set; } = [];
}

public sealed class OfflineSyncEvent : BaseEntity
{
    public long OfflineAuthorityLeaseId { get; set; }
    public OfflineAuthorityLease Lease { get; set; } = null!;
    public Guid EventId { get; set; }
    public long Sequence { get; set; }
    public string Kind { get; set; } = null!;
    public string IdempotencyKey { get; set; } = null!;
    public string PayloadHash { get; set; } = null!;
    public long ActorUserId { get; set; }
    public User ActorUser { get; set; } = null!;
    public string Status { get; set; } = "Applied";
    public long? ResultEntityId { get; set; }
    public string? ResultCode { get; set; }
    public DateTime DeviceOccurredAt { get; set; }
    public DateTime ProcessedAt { get; set; }
}
