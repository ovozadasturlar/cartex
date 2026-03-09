using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Roles.Queries;

public record GetRolesQuery : FilteringRequest, IRequest<IReadOnlyCollection<RoleDto>>;

public record RoleDto(long Id, string Name, string? Description, List<string> Permissions);

public sealed class GetRolesQueryHandler(
    IApplicationDbContext db,
    IPagingMetadataWriter writer) : IRequestHandler<GetRolesQuery, IReadOnlyCollection<RoleDto>>
{
    public async Task<IReadOnlyCollection<RoleDto>> Handle(GetRolesQuery request, CancellationToken cancellationToken)
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
            .ToPagedListAsync(request, writer, cancellationToken);
    }
}
