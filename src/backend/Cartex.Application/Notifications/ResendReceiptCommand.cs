using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Domain.Enums;
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
            .Select(c => new { c.Party.Phone, c.Party.Email, c.TelegramChatId, c.NotificationsOptOut, c.PreferredLanguage })
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

        var deliveries = config.Channels.Distinct()
            .Select(channel => (Channel: channel, Recipient: channel switch
            {
                NotificationChannel.Telegram => customer.TelegramChatId,
                NotificationChannel.Email => customer.Email,
                NotificationChannel.Sms => customer.Phone,
                _ => null
            }))
            .Where(x => !string.IsNullOrWhiteSpace(x.Recipient))
            .ToList();

        if (deliveries.Count == 0)
            throw new BusinessRuleException("Mijozda birorta kanal manzili yo'q (Telegram/email/telefon).");

        foreach (var (channel, recipient) in deliveries)
            await db.RunAfterCommitAsync(() => notifications.SendAsync(
                new NotificationMessage(channel, recipient!, "sale_receipt", payload, sale.CustomerId), cancellationToken));

        return Unit.Value;
    }
}
