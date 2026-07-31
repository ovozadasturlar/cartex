using Cartex.Domain.Common;
using Cartex.Domain.Enums;

namespace Cartex.Domain.Entities;

public class NotificationDelivery : BaseEntity
{
    public long? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public NotificationChannel Channel { get; set; }
    public string Purpose { get; set; } = null!;
    public string Recipient { get; set; } = null!;
    public string? Subject { get; set; }
    public string Content { get; set; } = null!;
    public NotificationDeliveryStatus Status { get; set; } = NotificationDeliveryStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AcceptedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public ICollection<NotificationDeliveryAttempt> Attempts { get; set; } = [];
}
