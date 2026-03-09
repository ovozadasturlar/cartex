using MediatR;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;

namespace Cartex.Application.Roles.Queries;

public record GetRolesQuery : IRequest<List<RoleDto>>;

public record RoleDto(long Id, string Name, string? Description, List<string> Permissions);

public sealed class GetRolesQueryHandler(IApplicationDbContext db) : IRequestHandler<GetRolesQuery, List<RoleDto>>
{
    public async Task<List<RoleDto>> Handle(GetRolesQuery request, CancellationToken cancellationToken)
    {
        return await db.Roles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .Select(r => new RoleDto(
                r.Id,
                r.Name,
                r.Description,
                r.RolePermissions
                    .Where(rp => rp.Permission.IsEnabled)
                    .Select(rp => rp.Permission.Name)
                    .ToList()))
            .ToListAsync(cancellationToken);
    }
}
