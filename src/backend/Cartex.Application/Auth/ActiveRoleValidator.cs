using Cartex.Auth.Services;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Auth;

public sealed class ActiveRoleValidator(IApplicationDbContext db) : IActiveRoleValidator
{
    public async Task<bool> IsValidAsync(
        long userId,
        IReadOnlyCollection<string> tokenRoles,
        string? authorizationStamp,
        CancellationToken cancellationToken)
    {
        var userIsActive = await db.Users
            .AsNoTracking()
            .AnyAsync(user => user.Id == userId && user.IsActive, cancellationToken);
        if (!userIsActive)
            return false;

        var activeRoles = await db.Roles
            .AsNoTracking()
            .Include(role => role.RolePermissions)
                .ThenInclude(rolePermission => rolePermission.Permission)
            .Where(role => role.IsActive && role.UserRoles.Any(userRole => userRole.UserId == userId))
            .ToListAsync(cancellationToken);

        return activeRoles.Count > 0
            && activeRoles.Select(role => role.Name).ToHashSet(StringComparer.Ordinal).SetEquals(tokenRoles)
            && !string.IsNullOrWhiteSpace(authorizationStamp)
            && string.Equals(
                RoleAuthorizationStamp.Create(activeRoles),
                authorizationStamp,
                StringComparison.Ordinal);
    }
}
