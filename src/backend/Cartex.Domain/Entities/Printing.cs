using Cartex.Domain.Common;
using Cartex.Domain.Enums;

namespace Cartex.Domain.Entities;

public class PrintNode : AuditableEntity, IBranchScoped
{
    public long BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public string DeviceId { get; set; } = null!;
    public string CredentialHash { get; set; } = null!;
    public DateTime CredentialIssuedAt { get; set; } = DateTime.UtcNow;
    public string Name { get; set; } = null!;
    public string? ClientVersion { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsTrusted { get; set; }
    public bool HostEnabled { get; set; }
    public PrintNodeStatus Status { get; set; } = PrintNodeStatus.Offline;
    public DateTime? LastSeenAt { get; set; }
    public DateTime? LastConnectedAt { get; set; }
    public DateTime? LastDisconnectedAt { get; set; }
    public long? LastUserId { get; set; }
    public User? LastUser { get; set; }
    public string? LastClient { get; set; }
    public string? LastIpAddress { get; set; }
    public ICollection<PrinterEndpoint> Endpoints { get; set; } = [];
}

public class PrinterEndpoint : AuditableEntity
{
    public long PrintNodeId { get; set; }
    public PrintNode PrintNode { get; set; } = null!;
    public string StableKey { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string SystemName { get; set; } = null!;
    public PrintCapability Capabilities { get; set; }
    public PrinterEndpointStatus Status { get; set; } = PrinterEndpointStatus.Unknown;
    public bool IsEnabled { get; set; } = true;
    public string? ProfileJson { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public DateTime? LastSuccessAt { get; set; }
    public DateTime? LastFailureAt { get; set; }
    public int ConsecutiveFailures { get; set; }
}

public class PrintRoutingPolicy : AuditableEntity, IBranchScoped
{
    public long BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public PrintJobKind Kind { get; set; }
    public bool IsEnabled { get; set; }
    public PrintRoutingMode RoutingMode { get; set; } = PrintRoutingMode.LocalFirst;
    public bool AllowFallback { get; set; } = true;
    public PrintStickyMode StickyMode { get; set; } = PrintStickyMode.Duration;
    public int StickyDurationSeconds { get; set; } = 600;
    public long? StickyEndpointId { get; set; }
    public PrinterEndpoint? StickyEndpoint { get; set; }
    public DateTime? StickyUntil { get; set; }
    public int MaxCopies { get; set; } = 3;
    public int MaxJobsPerMinute { get; set; } = 20;
    public int MaxCopiesPerMinute { get; set; } = 30;
    public int AssignmentTimeoutSeconds { get; set; } = 20;
    public bool RequireTrustedNode { get; set; } = true;
    public bool RequireTrustedRequesterDevice { get; set; }
    // The automatic trigger is a branch policy. The selected OS printer and PDF
    // folder remain device-local and must never be copied between workstations.
    public bool AutoPrintOnSale { get; set; }
    public int DefaultCopies { get; set; } = 1;
    // Null means that the business-wide receipt template is inherited. When set,
    // it contains a complete ReceiptSettings snapshot for this branch.
    public string? ReceiptSettingsOverrideJson { get; set; }
    public long Revision { get; set; } = 1;
    public ICollection<PrintRouteTarget> Targets { get; set; } = [];
}

public class PrintRouteTarget : BaseEntity
{
    public long PrintRoutingPolicyId { get; set; }
    public PrintRoutingPolicy PrintRoutingPolicy { get; set; } = null!;
    public long PrinterEndpointId { get; set; }
    public PrinterEndpoint PrinterEndpoint { get; set; } = null!;
    public int Priority { get; set; }
    public bool IsEnabled { get; set; } = true;
}

public class PrintRequesterDevice : AuditableEntity, IBranchScoped
{
    public long BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public string DeviceId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Client { get; set; }
    public bool IsTrusted { get; set; }
    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    public long? LastUserId { get; set; }
    public User? LastUser { get; set; }
    public string? LastIpAddress { get; set; }
}

public class PrintJob : BaseEntity, IBranchScoped
{
    public long BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public PrintJobKind Kind { get; set; }
    public PrintJobStatus Status { get; set; } = PrintJobStatus.Pending;
    public string SourceType { get; set; } = null!;
    public string SourceId { get; set; } = null!;
    public string PayloadJson { get; set; } = null!;
    public string? IdempotencyKey { get; set; }
    public int Copies { get; set; } = 1;
    public bool IsReprint { get; set; }
    public string? Reason { get; set; }
    public long RequestedByUserId { get; set; }
    public User RequestedByUser { get; set; } = null!;
    public string? RequestedDeviceId { get; set; }
    public string? RequestedDeviceName { get; set; }
    public string? RequestedClient { get; set; }
    public string? RequestedIpAddress { get; set; }
    public string? RequestedUserAgent { get; set; }
    public string? CorrelationId { get; set; }
    public long? OriginNodeId { get; set; }
    public PrintNode? OriginNode { get; set; }
    public long? AssignedNodeId { get; set; }
    public PrintNode? AssignedNode { get; set; }
    public long? AssignedEndpointId { get; set; }
    public PrinterEndpoint? AssignedEndpoint { get; set; }
    public string? LeaseToken { get; set; }
    public DateTime? LeaseExpiresAt { get; set; }
    public int AttemptCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AssignedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public ICollection<PrintAttempt> Attempts { get; set; } = [];
}

public class PrintAttempt : BaseEntity
{
    public long PrintJobId { get; set; }
    public PrintJob PrintJob { get; set; } = null!;
    public int AttemptNumber { get; set; }
    public long PrintNodeId { get; set; }
    public PrintNode PrintNode { get; set; } = null!;
    public long PrinterEndpointId { get; set; }
    public PrinterEndpoint PrinterEndpoint { get; set; } = null!;
    public PrintAttemptStatus Status { get; set; } = PrintAttemptStatus.Assigned;
    public string LeaseToken { get; set; } = null!;
    public string? SpoolJobId { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AcceptedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
}
