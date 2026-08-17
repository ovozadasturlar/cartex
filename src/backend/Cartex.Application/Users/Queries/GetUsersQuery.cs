using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Application.Common.Security;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Cartex.Shared.Models.Users;

namespace Cartex.Application.Users.Queries;

public record GetUsersQuery : FilteringRequest, IRequest<IReadOnlyCollection<UserDto>>;

public sealed class GetUsersQueryHandler(
    IApplicationDbContext db,
    IAccessControlService accessControl,
    IPagingMetadataWriter writer) : IRequestHandler<GetUsersQuery, IReadOnlyCollection<UserDto>>
{
    public async Task<IReadOnlyCollection<UserDto>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SortBy))
        {
            request.SortBy = "Id";
            request.Descending = false;
        }
        var ctx = await accessControl.GetContextAsync(cancellationToken);

        var query = db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.UserBranches)
            .Include(u => u.DefaultBranch)
            .AsQueryable();

        if (!ctx.AccessAll)
            query = query.Where(u => !u.UserRoles.Any(ur => ur.Role.AccessAll || ur.Role.Level > ctx.Level));

        return await query
            .ToPagedListAsync(request,
                u => new UserDto(
                    u.Id, u.FullName, u.Username,
                    u.UserRoles.Select(ur => ur.RoleId).ToList(),
                    u.UserRoles.Select(ur => ur.Role.Name).ToList(),
                    u.DefaultBranchId,
                    u.DefaultBranch != null ? u.DefaultBranch.Name : null,
                    u.UserBranches.Select(ub => ub.BranchId).ToList(),
                    u.StartPage, u.CartDestination, u.IsActive),
                writer, cancellationToken);
    }
}
