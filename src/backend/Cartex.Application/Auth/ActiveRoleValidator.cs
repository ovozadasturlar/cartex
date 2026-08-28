using Cartex.Auth.Services;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Cartex.Application.Auth;

public sealed class ActiveRoleValidator(IApplicationDbContext db, IMemoryCache cache) : IActiveRoleValidator
{
    public async Task<bool> IsValidAsync(
        long userId,
        IReadOnlyCollection<string> tokenRoles,
        string? authorizationStamp,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(authorizationStamp))
            return false;

        var cacheKey = $"auth:active-role:{userId}";

        var (isActive, stamp, roles) = await cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);

            var passwordHash = await db.Users
                .AsNoTracking()
                .Where(user => user.Id == userId && user.IsActive)
                .Select(user => user.PasswordHash)
                .FirstOrDefaultAsync(cancellationToken);

            if (passwordHash is null)
                return (false, string.Empty, (HashSet<string>?)null);

            var activeRoles = await db.Roles
                .AsNoTracking()
                .Include(role => role.RolePermissions)
                    .ThenInclude(rolePermission => rolePermission.Permission)
                .Where(role => role.IsActive && role.UserRoles.Any(userRole => userRole.UserId == userId))
                .ToListAsync(cancellationToken);

            var roleNames = activeRoles.Select(role => role.Name).ToHashSet(StringComparer.Ordinal);
            var computedStamp = RoleAuthorizationStamp.Create(passwordHash, activeRoles);

            return (true, computedStamp, roleNames);
        });

        return isActive
            && roles is not null
            && roles.Count > 0
            && roles.SetEquals(tokenRoles)
            && string.Equals(stamp, authorizationStamp, StringComparison.Ordinal);
    }
}
