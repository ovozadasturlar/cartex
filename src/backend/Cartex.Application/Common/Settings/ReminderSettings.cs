namespace Cartex.Application.Common.Settings;

public sealed class ReminderSettings
{
    public bool Enabled { get; set; }
    public int MinDaysOverdue { get; set; } = 7;
    public int RepeatEveryDays { get; set; } = 7;
    public decimal MinBalance { get; set; }
    public int SendHourLocal { get; set; } = 10;
    public bool NotifyBeforeDue { get; set; } = true;
    public int DaysBeforeDue { get; set; } = 1;
    public bool NotifyOnDueDate { get; set; } = true;
    public List<Cartex.Domain.Enums.NotificationChannel> Channels { get; set; } = [];
    public string? OverdueTemplate { get; set; }
    public string? DueSoonTemplate { get; set; }
    public string? DueTodayTemplate { get; set; }
}
