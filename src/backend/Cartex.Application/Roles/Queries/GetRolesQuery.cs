using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Application.Common.Security;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Cartex.Domain.Authorization;

namespace Cartex.Application.Roles.Queries;

public record GetRolesQuery : FilteringRequest, IRequest<IReadOnlyCollection<RoleDto>>;

public record RoleDto(long Id, string Name, string? Description, string? StartPage, int Priority, bool IsSystem, bool IsActive, bool AccessAll, List<string> Permissions, List<string> GrantablePermissions, List<string> AssignableRoles, string? CartDestination)
{
    public bool RequiresBranch { get; init; }
}

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
                    r.IsSystem,
                    r.IsActive,
                    r.AccessAll,
                    r.RolePermissions
                        .Where(rp => rp.Permission.IsEnabled)
                        .Select(rp => rp.Permission.Name)
                        .ToList(),
                    r.GrantablePermissions,
                    r.AssignableRoles,
                    r.CartDestination),
                writer, cancellationToken);

        return roles.Select(role => role with
        {
            RequiresBranch = role.AccessAll || role.Permissions.Any(name =>
                AppPermissions.Definitions.TryGetValue(name, out var definition)
                && definition.RequiresBranch)
        }).ToList();
    }
}
