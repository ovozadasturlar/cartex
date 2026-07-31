using Cartex.Domain.Common;
using Cartex.Domain.Enums;

namespace Cartex.Domain.Entities;

public class NotificationDeliveryAttempt : BaseEntity
{
    public long NotificationDeliveryId { get; set; }
    public NotificationDelivery NotificationDelivery { get; set; } = null!;
    public int AttemptNumber { get; set; }
    public string Provider { get; set; } = null!;
    public string? ProviderMessageId { get; set; }
    public NotificationDeliveryStatus Status { get; set; } = NotificationDeliveryStatus.Pending;
    public int Units { get; set; } = 1;
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AcceptedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
