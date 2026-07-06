namespace Cartex.Application.Common.Settings;

public sealed class ReminderSettings
{
    public bool Enabled { get; set; }
    public int MinDaysOverdue { get; set; } = 7;
    public int RepeatEveryDays { get; set; } = 7;
    public decimal MinBalance { get; set; }
    public int SendHourLocal { get; set; } = 10;
    public List<Cartex.Application.Common.Interfaces.NotificationChannel> Channels { get; set; } = [];
}
