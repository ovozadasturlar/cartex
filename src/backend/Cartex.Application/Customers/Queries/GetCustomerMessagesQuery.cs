using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Cartex.Application.Notifications.Queries;
using Cartex.Shared.Models.Notifications;

namespace Cartex.Application.Customers.Queries;

public record GetCustomerMessagesQuery(long CustomerId) : IRequest<IReadOnlyCollection<NotificationDeliveryDto>?>;

public sealed class GetCustomerMessagesQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ISender sender) : IRequestHandler<GetCustomerMessagesQuery, IReadOnlyCollection<NotificationDeliveryDto>?>
{
    public async Task<IReadOnlyCollection<NotificationDeliveryDto>?> Handle(GetCustomerMessagesQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(AppPermissions.Notifications.JournalView))
            throw new ForbiddenException("Xabarlar jurnalini ko'rish huquqi yo'q.");

        var customers = db.Customers.Where(c => c.Id == request.CustomerId);
        if (!currentUser.HasPermission(AppPermissions.Customers.ViewAll))
            customers = customers.Where(c => c.AssignedUserId == currentUser.UserId);
        if (!await customers.AnyAsync(cancellationToken))
            return null;

        return await sender.Send(new GetNotificationJournalQuery
        {
            CustomerId = request.CustomerId,
            Channel = "Sms",
            Page = 1,
            PageSize = 100
        }, cancellationToken);
    }
}
