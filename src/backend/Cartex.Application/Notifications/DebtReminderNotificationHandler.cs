using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Domain.Events;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Notifications;

public sealed class DebtReminderNotificationHandler(
    IApplicationDbContext db,
    ISettingsService settings,
    INotificationService notifications)
    : INotificationHandler<DomainEventNotification<DebtReminderDueEvent>>
{
    public async Task Handle(DomainEventNotification<DebtReminderDueEvent> notification, CancellationToken cancellationToken)
    {
        var reminder = notification.DomainEvent;

        var config = await settings.GetAsync<ReminderSettings>(SettingKeys.Reminder, cancellationToken);
        if (config is null || !config.Enabled || config.Channels.Count == 0)
            return;

        var customer = await db.Customers
            .Where(c => c.Id == reminder.CustomerId)
            .Select(c => new { c.Party.Phone, c.Party.Email, c.TelegramChatId })
            .FirstOrDefaultAsync(cancellationToken);
        if (customer is null)
            return;

        var payload = new Dictionary<string, string>
        {
            ["name"] = reminder.CustomerName,
            ["balance"] = reminder.Balance.ToString("0.##"),
            ["currency"] = reminder.Currency,
            ["days"] = reminder.DaysOverdue.ToString(),
            ["dueDate"] = reminder.DueDate?.ToString("dd.MM.yyyy") ?? ""
        };

        foreach (var channel in config.Channels.Distinct())
        {
            var recipient = channel switch
            {
                NotificationChannel.Telegram => customer.TelegramChatId,
                NotificationChannel.Email => customer.Email,
                NotificationChannel.Sms => customer.Phone,
                _ => null
            };
            if (string.IsNullOrWhiteSpace(recipient))
                continue;
            await notifications.SendAsync(new NotificationMessage(channel, recipient, reminder.Template, payload, reminder.CustomerId), cancellationToken);
        }
    }
}
