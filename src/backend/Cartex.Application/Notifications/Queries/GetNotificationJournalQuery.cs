using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Cartex.Shared.Models.Notifications;

namespace Cartex.Application.Notifications.Queries;

public record GetNotificationJournalQuery : FilteringRequest, IRequest<IReadOnlyCollection<NotificationDeliveryDto>>
{
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public string? Channel { get; init; }
    public string? Provider { get; init; }
    public string? Status { get; init; }
    public string? Purpose { get; init; }
}

public sealed class GetNotificationJournalQueryHandler(
    IApplicationDbContext db,
    IPagingMetadataWriter writer,
    ICurrentUser currentUser)
    : IRequestHandler<GetNotificationJournalQuery, IReadOnlyCollection<NotificationDeliveryDto>>
{
    public Task<IReadOnlyCollection<NotificationDeliveryDto>> Handle(GetNotificationJournalQuery request, CancellationToken cancellationToken)
    {
        var query = NotificationJournalFilters.Apply(db.NotificationDeliveries.AsNoTracking(), request);
        var sensitive = currentUser.HasPermission(AppPermissions.Notifications.JournalSensitive);
        var provider = request.Provider;

        return query
            .OrderByDescending(x => x.CreatedAt)
            .ToPagedListAsync(request,
                d => new NotificationDeliveryDto(
                    d.Id,
                    d.CustomerId,
                    d.Customer != null ? d.Customer.FullName : null,
                    d.Channel.ToString(),
                    d.Purpose,
                    sensitive ? d.Recipient : "••••",
                    d.Subject,
                    sensitive ? d.Content : null,
                    d.Status.ToString(),
                    d.Attempts
                        .Where(a => provider == null || a.Provider == provider)
                        .OrderByDescending(a => a.AttemptNumber)
                        .Select(a => a.Provider)
                        .FirstOrDefault() ?? "",
                    d.Attempts
                        .Where(a => provider == null || a.Provider == provider)
                        .OrderByDescending(a => a.AttemptNumber)
                        .Select(a => a.ProviderMessageId)
                        .FirstOrDefault(),
                    d.Attempts.Count(a => provider == null || a.Provider == provider),
                    d.Attempts
                        .Where(a => (provider == null || a.Provider == provider)
                            && (a.Status == NotificationDeliveryStatus.Accepted
                                || a.Status == NotificationDeliveryStatus.Delivered
                                || a.Status == NotificationDeliveryStatus.Undelivered))
                        .Sum(a => a.Units),
                    sensitive
                        ? d.Attempts
                            .Where(a => provider == null || a.Provider == provider)
                            .OrderByDescending(a => a.AttemptNumber)
                            .Select(a => a.ErrorMessage)
                            .FirstOrDefault()
                        : null,
                    d.CreatedAt,
                    d.AcceptedAt,
                    d.DeliveredAt,
                    d.CompletedAt),
                writer,
                cancellationToken);
    }
}

internal static class NotificationJournalFilters
{
    public static IQueryable<NotificationDelivery> Apply(IQueryable<NotificationDelivery> query, GetNotificationJournalQuery request)
    {
        if (request.From is { } from)
        {
            var fromUtc = NormalizeUtc(from);
            query = query.Where(x => x.CreatedAt >= fromUtc);
        }
        if (request.To is { } to)
        {
            var toUtc = NormalizeUtc(to);
            query = query.Where(x => x.CreatedAt < toUtc);
        }
        if (Enum.TryParse<NotificationChannel>(request.Channel, true, out var channel))
            query = query.Where(x => x.Channel == channel);
        if (Enum.TryParse<NotificationDeliveryStatus>(request.Status, true, out var status))
            query = query.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(request.Provider))
            query = query.Where(x => x.Attempts.Any(a => a.Provider == request.Provider));
        if (!string.IsNullOrWhiteSpace(request.Purpose))
            query = query.Where(x => x.Purpose == request.Purpose);
        return query;
    }

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
