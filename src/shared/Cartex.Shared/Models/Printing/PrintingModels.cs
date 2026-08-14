using System.Text.Json;
using Cartex.Shared.Models.Settings;

namespace Cartex.Shared.Models.Printing;

[Flags]
public enum PrintCapability
{
    None = 0,
    Receipt = 1,
    BarcodeLabel = 2,
    ZReport = 4,
    Document = 8
}

public enum PrintJobKind
{
    Receipt,
    BarcodeLabel,
    ZReport,
    Document
}

public enum PrintNodeStatus
{
    Offline,
    Online,
    Degraded
}

public enum PrinterEndpointStatus
{
    Unknown,
    Ready,
    Busy,
    Offline,
    Error
}

public enum PrintRoutingMode
{
    LocalFirst,
    PriorityOnly,
    LocalOnly
}

public enum PrintStickyMode
{
    Disabled,
    Duration,
    UntilFailure,
    Permanent
}

public enum PrintJobStatus
{
    Pending,
    Assigned,
    Accepted,
    SpoolSubmitted,
    Completed,
    Failed,
    ManualReview,
    Cancelled,
    Rejected
}

public enum PrintAttemptStatus
{
    Assigned,
    Accepted,
    SpoolSubmitted,
    Completed,
    FailedBeforeSubmit,
    UnknownAfterSubmit
}

public sealed record PrinterEndpointRegistration(
    string StableKey,
    string SystemName,
    string DisplayName,
    PrintCapability Capabilities,
    PrinterEndpointStatus Status,
    string? ProfileJson = null);

public sealed record RegisterPrintNodeRequest(
    string DeviceId,
    string DeviceName,
    long BranchId,
    string? ClientVersion,
    bool HostEnabled,
    IReadOnlyList<PrinterEndpointRegistration> Endpoints,
    string? HostToken = null);

public enum PrintNodeEnrollment
{
    Active,
    PendingApproval
}

public sealed record RegisterPrintNodeResult(
    PrintNodeDto Node,
    string? HostToken,
    PrintNodeEnrollment Enrollment = PrintNodeEnrollment.Active,
    string? Fingerprint = null);

public sealed record PrintNodeHeartbeatRequest(
    string DeviceId,
    IReadOnlyList<PrinterEndpointRegistration> Endpoints,
    string HostToken);

public sealed record PrinterEndpointDto(
    long Id,
    long PrintNodeId,
    string StableKey,
    string SystemName,
    string DisplayName,
    PrintCapability Capabilities,
    PrinterEndpointStatus Status,
    bool IsEnabled,
    DateTime? LastSeenAt,
    DateTime? LastSuccessAt,
    DateTime? LastFailureAt,
    int ConsecutiveFailures,
    string? ProfileJson);

public sealed record PrintNodeDto(
    long Id,
    long BranchId,
    string DeviceId,
    string Name,
    string? ClientVersion,
    bool IsEnabled,
    bool IsTrusted,
    bool HostEnabled,
    PrintNodeStatus Status,
    DateTime? LastSeenAt,
    string? LastClient,
    IReadOnlyList<PrinterEndpointDto> Endpoints,
    DateTime? PendingRequestedAt = null,
    string? PendingFingerprint = null,
    string? PendingClient = null,
    string? PendingIpAddress = null)
{
    public bool HasPendingEnrollment => PendingRequestedAt is not null;
}

public sealed record SetPrintNodeStateRequest(bool IsEnabled);

public sealed record PrintRequesterDeviceDto(
    long Id,
    long BranchId,
    string DeviceId,
    string Name,
    string? Client,
    bool IsTrusted,
    DateTime FirstSeenAt,
    DateTime LastSeenAt,
    string? LastUsername);

public sealed record SetPrintRequesterDeviceTrustRequest(bool IsTrusted);

public sealed record SetPrinterEndpointRequest(
    bool IsEnabled,
    PrintCapability Capabilities,
    string? DisplayName,
    string? ProfileJson);

public sealed record PrintRouteTargetDto(
    long EndpointId,
    string DeviceName,
    string PrinterName,
    PrintCapability Capabilities,
    int Priority,
    bool IsEnabled,
    bool IsTrusted,
    PrinterEndpointStatus Status);

public sealed record PrintRoutingPolicyDto(
    long Id,
    long BranchId,
    PrintJobKind Kind,
    bool IsEnabled,
    PrintRoutingMode RoutingMode,
    bool AllowFallback,
    PrintStickyMode StickyMode,
    int StickyDurationSeconds,
    long? StickyEndpointId,
    DateTime? StickyUntil,
    int MaxCopies,
    int MaxJobsPerMinute,
    int MaxCopiesPerMinute,
    int AssignmentTimeoutSeconds,
    bool RequireTrustedNode,
    bool RequireTrustedRequesterDevice,
    IReadOnlyList<PrintRouteTargetDto> Targets,
    bool AutoPrintOnSale = false,
    int DefaultCopies = 1,
    ReceiptSettingsDto? ReceiptOverride = null,
    long Revision = 1);

public sealed record PrintRouteTargetRequest(long EndpointId, int Priority, bool IsEnabled);

public sealed record UpdatePrintRoutingPolicyRequest(
    bool IsEnabled,
    PrintRoutingMode RoutingMode,
    bool AllowFallback,
    PrintStickyMode StickyMode,
    int StickyDurationSeconds,
    int MaxCopies,
    int MaxJobsPerMinute,
    int MaxCopiesPerMinute,
    int AssignmentTimeoutSeconds,
    bool RequireTrustedNode,
    bool RequireTrustedRequesterDevice,
    IReadOnlyList<PrintRouteTargetRequest> Targets);

/// <summary>
/// Updates only the receipt trigger/template portion of the branch policy. It is
/// intentionally separate from printer routing so a receipt editor cannot
/// accidentally replace route targets and a route edit cannot erase branding.
/// </summary>
public sealed record UpdateReceiptPrintPolicyRequest(
    bool AutoPrintOnSale,
    int DefaultCopies,
    bool UseBranchOverride = false,
    ReceiptSettingsDto? BranchOverride = null,
    long? ExpectedRevision = null);

public sealed record PrintingBootstrapDto(
    long BranchId,
    string? DeviceId,
    ReceiptSettingsDto BusinessReceipt,
    ReceiptSettingsDto EffectiveReceipt,
    PrintRoutingPolicyDto ReceiptPolicy,
    PrintNodeDto? DeviceNode,
    string Revision,
    DateTime ServerTimeUtc);

public sealed record CreatePrintJobRequest(
    long BranchId,
    PrintJobKind Kind,
    string SourceType,
    string SourceId,
    JsonElement Payload,
    int Copies = 1,
    bool IsReprint = false,
    string? Reason = null,
    string? IdempotencyKey = null,
    string? DeviceId = null,
    string? DeviceName = null);

public sealed record PrintJobDto(
    long Id,
    long BranchId,
    PrintJobKind Kind,
    PrintJobStatus Status,
    string SourceType,
    string SourceId,
    int Copies,
    bool IsReprint,
    string? Reason,
    long RequestedByUserId,
    string? RequestedDeviceId,
    string? RequestedDeviceName,
    string? RequestedClient,
    long? AssignedNodeId,
    long? AssignedEndpointId,
    int AttemptCount,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    string? ErrorCode,
    string? ErrorMessage,
    string? Summary = null,
    string? RequestedByName = null,
    string? AssignedDeviceName = null,
    string? AssignedPrinterName = null);

public sealed record PrintJobStatusUpdate(
    long JobId,
    PrintJobKind Kind,
    PrintJobStatus Status,
    string? PrinterName,
    string? ErrorMessage);

public sealed record AssignedPrintJobDto(
    long Id,
    long BranchId,
    PrintJobKind Kind,
    string SourceType,
    string SourceId,
    JsonElement Payload,
    int Copies,
    bool IsReprint,
    string LeaseToken,
    DateTime LeaseExpiresAt,
    long EndpointId,
    string PrinterSystemName,
    string PrinterDisplayName,
    string? PrinterProfileJson);

public sealed record PrintJobLeaseRequest(string DeviceId, string LeaseToken, string HostToken);
public sealed record PrintJobSubmittedRequest(string DeviceId, string LeaseToken, string HostToken, string? SpoolJobId);
public sealed record PrintJobFailedRequest(string DeviceId, string LeaseToken, string HostToken, string ErrorCode, string ErrorMessage, bool WasSubmitted);
