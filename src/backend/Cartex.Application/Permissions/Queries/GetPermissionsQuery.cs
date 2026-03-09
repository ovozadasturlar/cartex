using MediatR;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;

namespace Cartex.Application.Permissions.Queries;

public record GetPermissionsQuery : IRequest<List<PermissionDto>>;

public record PermissionDto(long Id, string Name, string? Description, bool IsEnabled);

public sealed class GetPermissionsQueryHandler(IApplicationDbContext db) : IRequestHandler<GetPermissionsQuery, List<PermissionDto>>
{
    public async Task<List<PermissionDto>> Handle(GetPermissionsQuery request, CancellationToken cancellationToken)
    {
        return await db.Permissions
            .Select(p => new PermissionDto(p.Id, p.Name, p.Description, p.IsEnabled))
            .ToListAsync(cancellationToken);
    }
}
