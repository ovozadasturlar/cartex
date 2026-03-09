using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.AuditLogs.Queries;

public record GetAuditLogsQuery : FilteringRequest, IRequest<IReadOnlyCollection<AuditLogDto>>;

public record AuditLogDto(
    long Id,
    string? UserName,
    string Action,
    string TableName,
    long? RecordId,
    string? OldData,
    string? NewData,
    DateTime CreatedAt);

public sealed class GetAuditLogsQueryHandler(
    IApplicationDbContext db,
    IPagingMetadataWriter writer) : IRequestHandler<GetAuditLogsQuery, IReadOnlyCollection<AuditLogDto>>
{
    public async Task<IReadOnlyCollection<AuditLogDto>> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        return await db.AuditLogs
            .Include(a => a.User)
            .ToPagedListAsync(request,
                a => new AuditLogDto(
                    a.Id,
                    a.User != null ? a.User.FullName : null,
                    a.Action,
                    a.TableName,
                    a.RecordId,
                    a.OldData,
                    a.NewData,
                    a.CreatedAt),
                writer, cancellationToken);
    }
}
