using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Models;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Settings.Queries;

public record SmsMessageDto(long Id, string Phone, string Text, string Provider, string Status, int Segments, string? Error, DateTime CreatedAt, DateTime? DeliveredAt);

public record GetSmsJournalQuery : FilteringRequest, IRequest<IReadOnlyCollection<SmsMessageDto>>;

public sealed class GetSmsJournalQueryHandler(IApplicationDbContext db, IPagingMetadataWriter writer)
    : IRequestHandler<GetSmsJournalQuery, IReadOnlyCollection<SmsMessageDto>>
{
    public Task<IReadOnlyCollection<SmsMessageDto>> Handle(GetSmsJournalQuery request, CancellationToken cancellationToken) =>
        db.SmsMessages
            .OrderByDescending(m => m.Id)
            .ToPagedListAsync(request,
                m => new SmsMessageDto(m.Id, m.Phone, m.Text, m.Provider, m.Status.ToString(), m.Segments, m.Error, m.CreatedAt, m.DeliveredAt),
                writer, cancellationToken);
}

public record SmsStatsDto(int Total, int Sent, int Delivered, int Undelivered, int Failed, int Segments);

public record GetSmsStatsQuery(DateTime? From = null, DateTime? To = null) : IRequest<SmsStatsDto>;

public sealed class GetSmsStatsQueryHandler(IApplicationDbContext db) : IRequestHandler<GetSmsStatsQuery, SmsStatsDto>
{
    public async Task<SmsStatsDto> Handle(GetSmsStatsQuery request, CancellationToken cancellationToken)
    {
        var query = db.SmsMessages.AsQueryable();
        if (request.From is { } from) query = query.Where(m => m.CreatedAt >= from);
        if (request.To is { } to) query = query.Where(m => m.CreatedAt < to);

        var rows = await query
            .GroupBy(m => m.Status)
            .Select(g => new { Status = g.Key, Count = g.Count(), Segments = g.Sum(m => m.Segments) })
            .ToListAsync(cancellationToken);

        int Count(SmsStatus s) => rows.FirstOrDefault(r => r.Status == s)?.Count ?? 0;
        return new SmsStatsDto(
            rows.Sum(r => r.Count),
            Count(SmsStatus.Sent),
            Count(SmsStatus.Delivered),
            Count(SmsStatus.Undelivered),
            Count(SmsStatus.Failed),
            rows.Where(r => r.Status != SmsStatus.Failed).Sum(r => r.Segments));
    }
}
