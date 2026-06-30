using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;
using Cartex.Auth.Services;
using Cartex.Application.Common.Interfaces;
using Cartex.Domain.Authorization;
using Cartex.Domain.Entities;
using Cartex.Application.Auth.Commands;

namespace Cartex.Application.Auth;

public sealed class AuthTokenBuilder(
    IApplicationDbContext db,
    IJwtTokenGenerator jwtTokenGenerator,
    ILicenseService licenseService)
{
    public Task<User?> LoadUserAsync(string username, CancellationToken cancellationToken) =>
        db.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .Include(u => u.UserBranches)
            .FirstOrDefaultAsync(u => u.Username == username, cancellationToken);

    public async Task<LoginResponse> BuildAsync(User user, CancellationToken cancellationToken)
    {
        var roles = user.UserRoles.Select(ur => ur.Role).OrderByDescending(r => r.Priority).ToList();
        var roleNames = roles.Select(r => r.Name).ToList();

        var permissions = roles.Any(r => r.AccessAll)
            ? new List<string> { AppPermissions.Wildcard }
            : roles
                .SelectMany(r => r.RolePermissions)
                .Where(rp => rp.Permission.IsEnabled)
                .Select(rp => rp.Permission.Name)
                .Distinct()
                .ToList();

        if (!roles.Any(r => r.AccessAll))
        {
            var disabledCodes = await db.Features.Where(f => !f.IsEnabled).Select(f => f.Code).ToListAsync(cancellationToken);
            var permittedFeatures = await licenseService.GetTariffFeaturesAsync(cancellationToken);
            var blockedFeatures = FeatureCatalog.AllCodes.Where(c => !permittedFeatures.Contains(c)).Concat(disabledCodes);
            var blocked = FeatureCatalog.PermissionsFor(blockedFeatures);
            if (blocked.Count > 0)
                permissions = permissions.Where(p => !blocked.Contains(p)).ToList();

            if (!await licenseService.IsActiveAsync(cancellationToken))
                permissions = permissions.Where(p => LicensePolicy.FreeModeAllowed.Contains(p)).ToList();
        }

        var startPage = !string.IsNullOrWhiteSpace(user.StartPage)
            ? user.StartPage
            : roles.Select(r => r.StartPage).FirstOrDefault(sp => !string.IsNullOrWhiteSpace(sp));

        var businessId = await db.Businesses.Select(b => b.Id).FirstAsync(cancellationToken);
        var branchIds = user.UserBranches.Select(ub => ub.BranchId).ToList();

        var token = jwtTokenGenerator.GenerateToken(
            user.Id, user.Username, user.FullName, roleNames, startPage, permissions,
            businessId, branchIds, user.DefaultBranchId);

        return new LoginResponse(token, user.FullName, roleNames.FirstOrDefault() ?? "");
    }
}
