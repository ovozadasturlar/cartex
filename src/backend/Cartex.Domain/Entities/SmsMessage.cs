using Cartex.Domain.Common;
using Cartex.Domain.Enums;

namespace Cartex.Domain.Entities;

public class SmsMessage : AuditableEntity
{
    public string Phone { get; set; } = null!;
    public string Text { get; set; } = null!;
    public string Provider { get; set; } = null!;
    public string? ProviderMessageId { get; set; }
    public SmsStatus Status { get; set; }
    public int Segments { get; set; }
    public string? Error { get; set; }
    public DateTime? DeliveredAt { get; set; }
}
