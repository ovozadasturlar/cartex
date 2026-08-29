using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Users.Commands;

internal static class UserBranchScopePolicy
{
    public static async Task<long?> ValidateAsync(
        IApplicationDbContext db,
        ICurrentUser currentUser,
        IReadOnlyCollection<long> roleIds,
        IReadOnlyCollection<long> requestedBranchIds,
        long? requestedDefaultBranchId,
        CancellationToken cancellationToken)
    {
        var roles = await db.Roles
            .Where(r => roleIds.Contains(r.Id))
            .Select(r => new
            {
                r.AccessAll,
                Permissions = r.RolePermissions
                    .Where(rp => rp.Permission.IsEnabled)
                    .Select(rp => rp.Permission.Name)
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        var permissionNames = roles.SelectMany(r => r.Permissions).ToHashSet();
        var canAccessAllBranches = roles.Any(r => r.AccessAll)
            || permissionNames.Contains(AppPermissions.Branches.ViewAll);
        var requiresBranch = roles.Any(r => r.AccessAll)
            || permissionNames.Any(name =>
                AppPermissions.Definitions.TryGetValue(name, out var definition)
                && definition.RequiresBranch);

        var branchIds = requestedBranchIds.Distinct().ToHashSet();
        if (!currentUser.CanAccessAllBranches
            && branchIds.Any(id => !currentUser.BranchIds.Contains(id)))
            throw new ForbiddenException("Siz o'zingizga biriktirilmagan filialni bera olmaysiz.");

        var existingBranchIds = await db.Branches
            .Where(b => branchIds.Contains(b.Id))
            .Select(b => b.Id)
            .ToListAsync(cancellationToken);
        if (existingBranchIds.Count != branchIds.Count)
            throw new BusinessRuleException("Tanlangan filiallardan biri mavjud emas.");

        if (requiresBranch && branchIds.Count == 0 && !canAccessAllBranches)
            throw new BusinessRuleException("Bu rol uchun kamida bitta filial tanlanishi shart.");

        if (requestedDefaultBranchId is { } defaultBranchId)
        {
            var defaultExists = await db.Branches.AnyAsync(b => b.Id == defaultBranchId, cancellationToken);
            if (!defaultExists)
                throw new BusinessRuleException("Asosiy filial mavjud emas.");
            if (!canAccessAllBranches && !branchIds.Contains(defaultBranchId))
                throw new BusinessRuleException("Asosiy filial ruxsat etilgan filiallar ichida bo'lishi shart.");
            return defaultBranchId;
        }

        if (branchIds.Count == 1)
            return branchIds.Single();

        if (requiresBranch && canAccessAllBranches)
            return await db.Branches.OrderBy(b => b.Id).Select(b => (long?)b.Id).FirstOrDefaultAsync(cancellationToken);

        if (requiresBranch)
            throw new BusinessRuleException("Bir nechta filialdan bittasini asosiy filial qilib tanlang.");

        return null;
    }
}
