using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Notifications.Queries;

public record NotificationJournalOptionsDto(
    List<string> Channels,
    List<string> Providers,
    List<string> Statuses,
    List<string> Purposes);

public record GetNotificationJournalOptionsQuery : IRequest<NotificationJournalOptionsDto>;

public sealed class GetNotificationJournalOptionsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetNotificationJournalOptionsQuery, NotificationJournalOptionsDto>
{
    public async Task<NotificationJournalOptionsDto> Handle(GetNotificationJournalOptionsQuery request, CancellationToken cancellationToken) =>
        new(
            Enum.GetNames<NotificationChannel>().ToList(),
            await db.NotificationDeliveryAttempts.Select(x => x.Provider).Distinct().OrderBy(x => x).ToListAsync(cancellationToken),
            Enum.GetNames<NotificationDeliveryStatus>().ToList(),
            await db.NotificationDeliveries.Select(x => x.Purpose).Distinct().OrderBy(x => x).ToListAsync(cancellationToken));
}
