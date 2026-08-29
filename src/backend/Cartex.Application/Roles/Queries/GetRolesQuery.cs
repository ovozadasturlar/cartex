using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Application.Common.Security;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Cartex.Domain.Authorization;
using Cartex.Shared.Models.Roles;

namespace Cartex.Application.Roles.Queries;

public record GetRolesQuery : FilteringRequest, IRequest<IReadOnlyCollection<RoleDto>>;

public sealed class GetRolesQueryHandler(
    IApplicationDbContext db,
    IAccessControlService accessControl,
    IPagingMetadataWriter writer) : IRequestHandler<GetRolesQuery, IReadOnlyCollection<RoleDto>>
{
    public async Task<IReadOnlyCollection<RoleDto>> Handle(GetRolesQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SortBy))
        {
            request.SortBy = "Id";
            request.Descending = false;
        }
        var ctx = await accessControl.GetContextAsync(cancellationToken);

        var query = db.Roles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .AsQueryable();

        if (!ctx.AccessAll)
            query = query.Where(r => !r.AccessAll && r.Level <= ctx.Level);

        var roles = await query
            .ToPagedListAsync(request,
                r => new RoleDto(
                    r.Id,
                    r.Name,
                    r.Description,
                    r.StartPage,
                    r.Priority,
                    r.AccessAll,
                    r.RolePermissions
                        .Where(rp => rp.Permission.IsEnabled)
                        .Select(rp => rp.Permission.Name)
                        .ToList(),
                    r.GrantablePermissions,
                    r.CartDestination)
                {
                    IsSystem = r.IsSystem,
                    IsActive = r.IsActive,
                    AssignableRoles = r.AssignableRoles
                },
                writer, cancellationToken);

        return roles.Select(role => role with
        {
            RequiresBranch = role.AccessAll || role.Permissions.Any(name =>
                AppPermissions.Definitions.TryGetValue(name, out var definition)
                && definition.RequiresBranch)
        }).ToList();
    }
}
