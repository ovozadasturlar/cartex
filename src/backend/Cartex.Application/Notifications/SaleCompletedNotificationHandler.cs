using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Domain.Events;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Notifications;

public sealed class SaleCompletedNotificationHandler(
    IApplicationDbContext db,
    ISettingsService settings,
    INotificationService notifications)
    : INotificationHandler<DomainEventNotification<SaleCompletedEvent>>
{
    public async Task Handle(DomainEventNotification<SaleCompletedEvent> notification, CancellationToken cancellationToken)
    {
        var sale = notification.DomainEvent;

        var config = await settings.GetAsync<NotificationSettings>(SettingKeys.Notification, cancellationToken);
        if (config is null || config.Channels.Count == 0)
            return;

        var customer = sale.CustomerId is null ? null : await db.Customers
            .Where(c => c.Id == sale.CustomerId)
            .Select(c => new { c.Phone, c.Email, c.TelegramChatId, c.NotificationsOptOut, c.PreferredLanguage })
            .FirstOrDefaultAsync(cancellationToken);
        if (customer is { NotificationsOptOut: true })
            customer = null;

        var payload = new Dictionary<string, string>
        {
            ["receiptToken"] = sale.ReceiptToken,
            ["total"] = sale.TotalAmount.ToString("0.##")
        };
        if (customer?.PreferredLanguage is { } lang)
            payload["lang"] = lang;

        async Task SendAsync(NotificationChannel channel, string? recipient)
        {
            if (string.IsNullOrWhiteSpace(recipient))
                return;
            await notifications.SendAsync(new NotificationMessage(channel, recipient, "sale_receipt", payload, sale.CustomerId), cancellationToken);
        }

        foreach (var channel in config.Channels.Distinct().Where(x => x != NotificationChannel.Sms))
        {
            var recipient = channel switch
            {
                NotificationChannel.Telegram => customer?.TelegramChatId,
                NotificationChannel.Email => customer?.Email,
                NotificationChannel.Sms => customer?.Phone,
                _ => null
            };
            await SendAsync(channel, recipient);
        }

        if (config.CopyToAdmin)
        {
            var telegram = await settings.GetAsync<TelegramSettings>(SettingKeys.Telegram, cancellationToken);
            await SendAsync(NotificationChannel.Telegram, telegram?.ChatId);
        }
    }
}
