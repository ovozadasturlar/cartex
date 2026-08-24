using Cartex.Domain.Common;
using Cartex.Domain.Enums;

namespace Cartex.Domain.Entities;

public sealed class SmsGatewayDevice : AuditableEntity, IBranchScoped
{
    public long BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public string DeviceId { get; set; } = null!;
    public string CredentialHash { get; set; } = null!;
    public DateTime CredentialIssuedAt { get; set; } = DateTime.UtcNow;
    public string DeviceName { get; set; } = null!;
    public string Client { get; set; } = null!;
    public int SimSlot { get; set; }
    public string SimOperator { get; set; } = null!;
    public string SimSubscriptionId { get; set; } = null!;
    public string? PhoneLabel { get; set; }
    public bool IsTrusted { get; set; }
    public bool IsConsented { get; set; }
    public bool IsEnabled { get; set; }
    public int? MonthlyQuota { get; set; }
    public int? ConsentedMonthlyQuota { get; set; }
    public int QuotaResetDay { get; set; } = 1;
    public int SentThisPeriod { get; set; }
    public DateTime PeriodStartedAt { get; set; } = DateTime.UtcNow;
    public int MaxPerHour { get; set; } = 60;
    public int ConsentedMaxPerHour { get; set; } = 60;
    public int MinIntervalSeconds { get; set; } = 4;
    public int ConsentedMinIntervalSeconds { get; set; } = 4;
    public int LowQuotaWarnPercent { get; set; } = 10;
    public DateTime? LowQuotaWarnedPeriodStartedAt { get; set; }
    public DateTime? PausedAt { get; set; }
    public DateTime? ConsentedAt { get; set; }
    public int Priority { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public DateTime? LastSentAt { get; set; }
    public string? LastError { get; set; }
    public long? LastUserId { get; set; }
    public User? LastUser { get; set; }
}

public sealed class SmsGatewayJob : BaseEntity, IBranchScoped
{
    public long BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public SmsGatewayJobKind Kind { get; set; }
    public string Phone { get; set; } = null!;
    public string Text { get; set; } = null!;
    public long? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public long? NotificationDeliveryId { get; set; }
    public NotificationDelivery? NotificationDelivery { get; set; }
    public long? NotificationDeliveryAttemptId { get; set; }
    public NotificationDeliveryAttempt? NotificationDeliveryAttempt { get; set; }
    public SmsGatewayJobStatus Status { get; set; } = SmsGatewayJobStatus.Pending;
    public long? AssignedDeviceId { get; set; }
    public SmsGatewayDevice? AssignedDevice { get; set; }
    public long? StickyDeviceId { get; set; }
    public SmsGatewayDevice? StickyDevice { get; set; }
    public long? RetryOfJobId { get; set; }
    public SmsGatewayJob? RetryOfJob { get; set; }
    public string? LeaseToken { get; set; }
    public DateTime? LeaseExpiresAt { get; set; }
    public int AttemptCount { get; set; }
    public int SegmentCount { get; set; } = 1;
    public string IdempotencyKey { get; set; } = null!;
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public string? WaitingReason { get; set; }
    public DateTime? AvailableAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AssignedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? SimulatedAt { get; set; }
    public string? FallbackProvider { get; set; }
    public string? FallbackMessageId { get; set; }
}

public sealed class CustomerSmsRoute : BaseEntity, IBranchScoped
{
    public long BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public long CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public long LastDeviceId { get; set; }
    public SmsGatewayDevice LastDevice { get; set; } = null!;
    public DateTime LastSentAt { get; set; }
}
