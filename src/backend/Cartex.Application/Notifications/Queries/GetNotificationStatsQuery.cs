using Cartex.Application.Common.Messaging;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Notifications.Queries;

public record NotificationStatsDto(
    int Deliveries,
    int Attempts,
    int Accepted,
    int Delivered,
    int Undelivered,
    int Failed,
    int Skipped,
    int BillableUnits);

public record GetNotificationStatsQuery(
    DateTime? From = null,
    DateTime? To = null,
    string? Channel = null,
    string? Provider = null,
    string? Status = null,
    string? Purpose = null) : IRequest<NotificationStatsDto>;

public sealed class GetNotificationStatsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetNotificationStatsQuery, NotificationStatsDto>
{
    public async Task<NotificationStatsDto> Handle(GetNotificationStatsQuery request, CancellationToken cancellationToken)
    {
        var filter = new GetNotificationJournalQuery
        {
            From = request.From,
            To = request.To,
            Channel = request.Channel,
            Provider = request.Provider,
            Status = request.Status,
            Purpose = request.Purpose
        };
        var deliveries = NotificationJournalFilters.Apply(db.NotificationDeliveries.AsNoTracking(), filter);
        var deliveryCount = await deliveries.CountAsync(cancellationToken);
        var attempts = deliveries.SelectMany(x => x.Attempts);
        if (!string.IsNullOrWhiteSpace(request.Provider))
            attempts = attempts.Where(x => x.Provider == request.Provider);
        if (Enum.TryParse<NotificationDeliveryStatus>(request.Status, true, out var status))
            attempts = attempts.Where(x => x.Status == status);

        var rows = await attempts
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count(), Units = g.Sum(x => x.Units) })
            .ToListAsync(cancellationToken);

        int Count(NotificationDeliveryStatus status) => rows.FirstOrDefault(x => x.Status == status)?.Count ?? 0;
        var billable = rows
            .Where(x => x.Status is NotificationDeliveryStatus.Accepted
                or NotificationDeliveryStatus.Delivered
                or NotificationDeliveryStatus.Undelivered)
            .Sum(x => x.Units);

        return new NotificationStatsDto(
            deliveryCount,
            rows.Sum(x => x.Count),
            Count(NotificationDeliveryStatus.Accepted),
            Count(NotificationDeliveryStatus.Delivered),
            Count(NotificationDeliveryStatus.Undelivered),
            Count(NotificationDeliveryStatus.Failed),
            Count(NotificationDeliveryStatus.Skipped),
            billable);
    }
}
