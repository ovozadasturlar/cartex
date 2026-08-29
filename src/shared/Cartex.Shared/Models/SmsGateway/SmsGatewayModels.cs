namespace Cartex.Shared.Models.SmsGateway;

public enum SmsGatewayJobKind
{
    DebtReminder,
    ReceiptLink,
    Promotion,
    Manual
}

public enum SmsGatewayJobStatus
{
    Pending,
    Assigned,
    Sent,
    Delivered,
    Simulated,
    Failed,
    Rejected,
    Cancelled
}

public sealed record SmsGatewayConsentScope(
    int? MonthlyQuota,
    bool IsUnlimited,
    int QuotaResetDay,
    int MaxPerHour,
    int MinIntervalSeconds,
    int LowQuotaWarnPercent);

public sealed record RegisterSmsGatewayRequest(
    long BranchId,
    string DeviceId,
    string DeviceName,
    string Client,
    int SimSlot,
    string SimOperator,
    string SimSubscriptionId,
    string? PhoneLabel,
    string? HostToken,
    SmsGatewayConsentScope? Consent);

public sealed record RegisterSmsGatewayResult(SmsGatewayDeviceDto Device, string? HostToken);

public sealed record SmsGatewayHeartbeatRequest(string DeviceId, int SimSlot, string HostToken, string? LastError);

public sealed record SmsGatewayHostRequest(string DeviceId, int SimSlot, string HostToken);

public sealed record SmsGatewayDeviceDto(
    long Id,
    long BranchId,
    string DeviceId,
    string DeviceName,
    string Client,
    int SimSlot,
    string SimOperator,
    string SimSubscriptionId,
    string? PhoneLabel,
    bool IsTrusted,
    bool IsConsented,
    bool IsEnabled,
    bool IsPaused,
    int? MonthlyQuota,
    int QuotaResetDay,
    int SentThisPeriod,
    int MaxPerHour,
    int MinIntervalSeconds,
    int Priority,
    DateTime? LastSeenAt,
    string? LastError,
    bool IsOnline,
    string BlockReason,
    DateTime QuotaResetsAt,
    int LowQuotaWarnPercent,
    bool IsOverQuota,
    DateTime? LastSentAt,
    int LinkedCustomerCount);

public sealed record UpdateSmsGatewayDeviceRequest(
    bool IsEnabled,
    int Priority);

public sealed record UpdateSmsGatewayConsentRequest(
    string DeviceId,
    int SimSlot,
    string HostToken,
    SmsGatewayConsentScope Scope,
    bool ConsentGranted);

public sealed record UpdateSmsGatewayConsentResult(bool RequiresConsent, SmsGatewayDeviceDto Device);

public sealed record SetSmsGatewayPauseRequest(string DeviceId, int SimSlot, string HostToken, bool IsPaused);

public sealed record SetSmsGatewayTrustRequest(bool IsTrusted);

public sealed record SetSmsGatewayConsentRequest(string DeviceId, int SimSlot, string HostToken, bool IsConsented);

public sealed record SmsGatewayHostStateRequest(string DeviceId, int SimSlot, string HostToken);

public sealed record SmsGatewayMessageTypesDto(bool DebtReminder, bool ReceiptLink, bool Manual, bool Promotion);

public sealed record SmsGatewayHostStateDto(
    SmsGatewayDeviceDto Device,
    IReadOnlyList<SmsGatewayJobDto> Jobs,
    SmsGatewayMessageTypesDto MessageTypes,
    bool TestMode);

public sealed record SendSmsGatewayTestRequest(string DeviceId, int SimSlot, string HostToken, string Phone);

public sealed record SmsGatewayJobDto(
    long Id,
    long BranchId,
    SmsGatewayJobKind Kind,
    string PhoneMasked,
    string? Phone,
    string Text,
    long? CustomerId,
    string? CustomerName,
    SmsGatewayJobStatus Status,
    long? AssignedDeviceId,
    string? DeviceLabel,
    int? SimSlot,
    int SegmentCount,
    int AttemptCount,
    string? ErrorCode,
    string? ErrorMessage,
    string? WaitingReason,
    DateTime? AvailableAt,
    long? NotificationDeliveryId,
    long? RetryOfJobId,
    DateTime CreatedAt,
    DateTime? SentAt,
    DateTime? DeliveredAt);

public sealed record AssignedSmsGatewayJobDto(
    long Id,
    SmsGatewayJobKind Kind,
    string Phone,
    string Text,
    int SegmentCount,
    string LeaseToken,
    DateTime LeaseExpiresAt,
    int MinIntervalSeconds,
    int MaxPerHour,
    bool Simulate);

public sealed record SmsGatewayLeaseRequest(string DeviceId, int SimSlot, string HostToken, string LeaseToken);

public sealed record SmsGatewayFailureRequest(string DeviceId, int SimSlot, string HostToken, string LeaseToken, string ErrorCode, string ErrorMessage);

public sealed record SmsGatewayJournalPeriodDto(int Sent, int Waiting, int Failed);

public sealed record SmsGatewayJournalSummaryDto(SmsGatewayJournalPeriodDto Today, SmsGatewayJournalPeriodDto ThisMonth);

public sealed record SmsGatewayJournalDto(IReadOnlyList<SmsGatewayJobDto> Jobs, SmsGatewayJournalSummaryDto Summary);

public sealed record SmsGatewayJobIdsRequest(IReadOnlyList<long> JobIds);

public sealed record ReassignSmsGatewayJobsRequest(IReadOnlyList<long> JobIds, long DeviceId);

public sealed record CreateSmsGatewayJobRequest(
    long BranchId,
    SmsGatewayJobKind Kind,
    string Phone,
    string Text,
    string IdempotencyKey,
    long? CustomerId = null);
