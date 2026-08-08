using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.AuditLogs.Queries;

public record GetAuditFilterOptionsQuery : IRequest<AuditFilterOptionsDto>;

public record AuditFilterOptionsDto(IReadOnlyList<string> Tables, IReadOnlyList<string> Actions, IReadOnlyList<string> Users);

public sealed class GetAuditFilterOptionsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetAuditFilterOptionsQuery, AuditFilterOptionsDto>
{
    public async Task<AuditFilterOptionsDto> Handle(GetAuditFilterOptionsQuery request, CancellationToken cancellationToken)
    {
        var tables = await db.AuditLogs.Select(a => a.TableName).Distinct().OrderBy(t => t).ToListAsync(cancellationToken);
        var actions = await db.AuditLogs.Select(a => a.Action).Distinct().OrderBy(a => a).ToListAsync(cancellationToken);
        var users = await db.AuditLogs.Where(a => a.User != null)
            .Select(a => a.User!.FullName).Distinct().OrderBy(u => u).ToListAsync(cancellationToken);
        return new AuditFilterOptionsDto(tables, actions, users);
    }
}
