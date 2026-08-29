using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Common.Security;

public sealed class AccessControlService(IApplicationDbContext db, ICurrentUser currentUser) : IAccessControlService
{
    private AccessContext? _context;

    public async Task<AccessContext> GetContextAsync(CancellationToken cancellationToken)
    {
        if (_context is not null) return _context;

        var userId = currentUser.UserId ?? throw new ForbiddenException("Not authenticated.");

        var roles = await db.Roles
            .Where(r => r.IsActive && r.UserRoles.Any(ur => ur.UserId == userId))
            .Select(r => new
            {
                r.Level,
                r.AccessAll,
                r.GrantablePermissions,
                r.AssignableRoles,
                Permissions = r.RolePermissions.Where(rp => rp.Permission.IsEnabled).Select(rp => rp.Permission.Name)
            })
            .ToListAsync(cancellationToken);

        var accessAll = roles.Any(r => r.AccessAll);
        var level = roles.Count == 0 ? 0 : roles.Max(r => r.Level);
        var permissions = roles.SelectMany(r => r.Permissions).ToHashSet();
        var grantable = roles.SelectMany(r => r.GrantablePermissions).ToHashSet();
        var assignable = roles.SelectMany(r => r.AssignableRoles).ToHashSet();

        return _context = new AccessContext(userId, level, accessAll, permissions, grantable, assignable);
    }

    public async Task EnsureCanCreateRoleAsync(int level, CancellationToken cancellationToken)
    {
        var ctx = await GetContextAsync(cancellationToken);
        if (ctx.AccessAll) return;
        if (level >= ctx.Level)
            throw new ForbiddenException("Cannot create a role at or above your own level.");
    }

    public async Task EnsureCanManageRoleAsync(Role role, CancellationToken cancellationToken)
    {
        var ctx = await GetContextAsync(cancellationToken);
        if (ctx.AccessAll) return;
        if (role.AccessAll || role.Level >= ctx.Level)
            throw new ForbiddenException("You cannot manage this role.");
    }

    public async Task EnsureCanGrantAsync(IReadOnlyCollection<long> permissionIds, CancellationToken cancellationToken)
    {
        var ctx = await GetContextAsync(cancellationToken);
        if (ctx.AccessAll) return;

        var names = await db.Permissions
            .Where(p => permissionIds.Contains(p.Id))
            .Select(p => p.Name)
            .ToListAsync(cancellationToken);

        if (names.Any(n => AppPermissions.DeveloperOnly.Contains(n) || !ctx.GrantablePermissions.Contains(n)))
            throw new ForbiddenException("You cannot grant permissions outside your grant scope.");
    }

    public async Task EnsureCanDelegateAsync(IReadOnlyCollection<string> permissionKeys, CancellationToken cancellationToken)
    {
        if (permissionKeys.Count == 0) return;
        var ctx = await GetContextAsync(cancellationToken);

        if (permissionKeys.Any(k => AppPermissions.DeveloperOnly.Contains(k)))
            throw new ForbiddenException("Developer-only permissions cannot be delegated.");

        if (!ctx.AccessAll && permissionKeys.Any(k => !ctx.GrantablePermissions.Contains(k)))
            throw new ForbiddenException("You cannot delegate permissions outside your grant scope.");
    }

    public async Task EnsureCanAssignRolesAsync(IReadOnlyCollection<long> roleIds, CancellationToken cancellationToken)
    {
        var ctx = await GetContextAsync(cancellationToken);

        var roles = await db.Roles
            .Where(r => roleIds.Contains(r.Id))
            .Select(r => new { r.Name, r.AccessAll, r.Level, r.IsActive })
            .ToListAsync(cancellationToken);

        if (roles.Count != roleIds.Distinct().Count() || roles.Any(r => !r.IsActive))
            throw new BusinessRuleException("Inactive or missing roles cannot be assigned.");

        if (ctx.AccessAll) return;

        if (roles.Any(r => r.AccessAll || r.Level >= ctx.Level))
            throw new ForbiddenException("You cannot assign roles at or above your own level.");

        if (ctx.AssignableRoles.Count > 0 && roles.Any(r => !ctx.AssignableRoles.Contains(r.Name)))
            throw new ForbiddenException("You cannot assign this role.");
    }

    public async Task EnsureCanManageUserAsync(User user, CancellationToken cancellationToken)
    {
        var ctx = await GetContextAsync(cancellationToken);
        if (ctx.AccessAll) return;

        var blocked = await db.Roles
            .AnyAsync(r => r.UserRoles.Any(ur => ur.UserId == user.Id) && (r.AccessAll || r.Level >= ctx.Level), cancellationToken);

        if (blocked)
            throw new ForbiddenException("You cannot manage this user.");
    }

    public async Task EnsureAdminRemainsAsync(long excludingUserId, CancellationToken cancellationToken)
    {
        var hasOther = await db.Users.AnyAsync(u =>
            u.Id != excludingUserId && u.IsActive &&
            u.UserRoles.Any(ur => ur.Role.IsActive && ur.Role.Level >= AppRoles.AdminLevel && !ur.Role.AccessAll),
            cancellationToken);

        if (!hasOther)
            throw new BusinessRuleException("Cannot remove the last administrator.");
    }
}
