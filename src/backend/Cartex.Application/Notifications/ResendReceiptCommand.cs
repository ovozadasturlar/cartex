using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Domain.Common.Exceptions;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Notifications;

public record ResendReceiptCommand(long SaleId) : ICommand<Unit>;

public sealed class ResendReceiptCommandHandler(
    IApplicationDbContext db,
    ISettingsService settings,
    INotificationService notifications) : IRequestHandler<ResendReceiptCommand, Unit>
{
    public async Task<Unit> Handle(ResendReceiptCommand request, CancellationToken cancellationToken)
    {
        var sale = await db.Sales
            .Where(s => s.Id == request.SaleId)
            .Select(s => new { s.ReceiptToken, s.TotalAmount, s.CustomerId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Savdo topilmadi.");

        if (sale.CustomerId is null)
            throw new BusinessRuleException("Bu savdoga mijoz biriktirilmagan.");

        var customer = await db.Customers
            .Where(c => c.Id == sale.CustomerId)
            .Select(c => new { c.Phone, c.Email, c.TelegramChatId, c.NotificationsOptOut, c.PreferredLanguage })
            .FirstAsync(cancellationToken);

        if (customer.NotificationsOptOut)
            throw new BusinessRuleException("Mijoz xabarnomalardan voz kechgan.");

        var config = await settings.GetAsync<NotificationSettings>(SettingKeys.Notification, cancellationToken);
        if (config is null || config.Channels.Count == 0)
            throw new BusinessRuleException("Xabar kanallari sozlanmagan.");

        var payload = new Dictionary<string, string>
        {
            ["receiptToken"] = sale.ReceiptToken,
            ["total"] = sale.TotalAmount.ToString("0.##")
        };
        if (customer.PreferredLanguage is { } lang)
            payload["lang"] = lang;

        var sent = 0;
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
            await notifications.SendAsync(new NotificationMessage(channel, recipient, "sale_receipt", payload), cancellationToken);
            sent++;
        }

        if (sent == 0)
            throw new BusinessRuleException("Mijozda birorta kanal manzili yo'q (Telegram/email/telefon).");

        return Unit.Value;
    }
}
