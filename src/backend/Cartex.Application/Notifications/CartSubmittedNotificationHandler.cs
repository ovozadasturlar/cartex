using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Domain.Events;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Notifications;

public sealed class CartSubmittedNotificationHandler(
    IApplicationDbContext db,
    ISettingsService settings,
    ITelegramService telegram)
    : INotificationHandler<DomainEventNotification<CartSubmittedEvent>>
{
    public async Task Handle(DomainEventNotification<CartSubmittedEvent> notification, CancellationToken cancellationToken)
    {
        var evt = notification.DomainEvent;

        var tg = await settings.GetAsync<TelegramSettings>(SettingKeys.Telegram, cancellationToken);
        if (tg is null || !tg.Enabled || string.IsNullOrWhiteSpace(tg.BotToken) || string.IsNullOrWhiteSpace(tg.ChatId))
            return;

        var customerName = evt.CustomerId is null ? null : await db.Customers
            .Where(c => c.Id == evt.CustomerId)
            .Select(c => c.FullName)
            .FirstOrDefaultAsync(cancellationToken);

        var text = $"🛒 Yangi onlayn buyurtma\nDo'kon: {evt.WarehouseName}\nMijoz: {customerName ?? "—"}\nMahsulot turlari: {evt.ItemCount}\nKod: {evt.AggregateCode}";
        await telegram.SendMessageAsync(tg.ChatId, text, cancellationToken);
    }
}
