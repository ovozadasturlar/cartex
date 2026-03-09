using MediatR;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;

namespace Cartex.Application.AuditLogs.Queries;

public record GetAuditLogsQuery(string? TableName, DateTime? FromDate, DateTime? ToDate) : IRequest<List<AuditLogDto>>;

public record AuditLogDto(
    long Id,
    string? UserName,
    string Action,
    string TableName,
    long? RecordId,
    string? OldData,
    string? NewData,
    DateTime CreatedAt);

public sealed class GetAuditLogsQueryHandler(IApplicationDbContext db) : IRequestHandler<GetAuditLogsQuery, List<AuditLogDto>>
{
    public async Task<List<AuditLogDto>> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var query = db.AuditLogs
            .Include(a => a.User)
            .AsQueryable();

        if (request.TableName is not null)
            query = query.Where(a => a.TableName == request.TableName);

        if (request.FromDate is not null)
            query = query.Where(a => a.CreatedAt >= request.FromDate);

        if (request.ToDate is not null)
            query = query.Where(a => a.CreatedAt <= request.ToDate);

        return await query
            .Select(a => new AuditLogDto(
                a.Id,
                a.User != null ? a.User.FullName : null,
                a.Action,
                a.TableName,
                a.RecordId,
                a.OldData,
                a.NewData,
                a.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
