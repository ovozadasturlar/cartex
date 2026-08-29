using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class DebtReminderLog : BaseEntity
{
    public long CustomerId { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public decimal Balance { get; set; }
    public int DaysOverdue { get; set; }
    public string Purpose { get; set; } = "debt_reminder";
    public DateOnly? DueDate { get; set; }
}
