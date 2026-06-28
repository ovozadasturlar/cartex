using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Domain.Events;
using Cartex.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Notifications;

public sealed class SaleCompletedNotificationHandler(IApplicationDbContext db, INotificationService notifications)
    : INotificationHandler<DomainEventNotification<SaleCompletedEvent>>
{
    public async Task Handle(DomainEventNotification<SaleCompletedEvent> notification, CancellationToken cancellationToken)
    {
        var sale = notification.DomainEvent;
        if (sale.CustomerId is null)
            return;

        var phone = await db.Customers
            .Where(c => c.Id == sale.CustomerId)
            .Select(c => c.Phone)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(phone))
            return;

        await notifications.SendAsync(new NotificationMessage(
            NotificationChannel.Telegram,
            phone,
            "sale_receipt",
            new Dictionary<string, string>
            {
                ["receiptToken"] = sale.ReceiptToken,
                ["total"] = sale.TotalAmount.ToString("0.##")
            }), cancellationToken);
    }
}
