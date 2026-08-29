namespace Cartex.Shared.Models.Notifications;

public record NotificationDeliveryDto(
    long Id,
    long? CustomerId,
    string? CustomerName,
    string Channel,
    string Purpose,
    string Recipient,
    string? Subject,
    string? Content,
    string Status,
    string Provider,
    string? ProviderMessageId,
    int AttemptCount,
    int Units,
    string? Error,
    DateTime CreatedAt,
    DateTime? AcceptedAt,
    DateTime? DeliveredAt,
    DateTime? CompletedAt,
    long? SmsGatewayJobId,
    long? DeviceId,
    string? DeviceLabel,
    int? SimSlot,
    string? WaitingReason);

public record NotificationStatsDto(
    int Deliveries,
    int Attempts,
    int Accepted,
    int Delivered,
    int Undelivered,
    int Failed,
    int Skipped,
    int BillableUnits);

public record NotificationJournalOptionsDto(
    List<string> Channels,
    List<string> Providers,
    List<string> Statuses,
    List<string> Purposes);

public record RecordNotificationExportRequest(
    DateTime? From,
    DateTime? To,
    string? Channel,
    string? Provider,
    string? Status,
    string? Purpose,
    string Format,
    int RowCount);
