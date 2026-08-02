using Cartex.Domain.Common;
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
    ILicenseService licenseService,
    IAuditService audit,
    ICurrentUser currentUser)
{
    private const int RefreshLifetimeDays = 30;
    private const int AbsoluteLifetimeDays = 90;
    private const int ReuseGraceSeconds = 30;

    private IQueryable<User> UsersWithGraph() =>
        db.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .Include(u => u.UserBranches);

    public Task<User?> LoadUserAsync(string username, CancellationToken cancellationToken) =>
        UsersWithGraph().FirstOrDefaultAsync(u => u.Username == username, cancellationToken);

    public Task<User?> LoadUserByIdAsync(long id, CancellationToken cancellationToken) =>
        UsersWithGraph().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task<LoginResponse> IssueAsync(User user, string? deviceName, string? deviceId, CancellationToken cancellationToken)
    {
        var (accessToken, role) = await BuildAccessAsync(user, cancellationToken);
        var now = DateTime.UtcNow;
        var refreshToken = CreateSession(user.Id, deviceName, deviceId, now, out _);
        audit.Add("login", "auth", user.Id, new { user.Username, Device = deviceName, DeviceId = deviceId }, asUserId: user.Id);
        await db.SaveChangesAsync(cancellationToken);
        return new LoginResponse(accessToken, refreshToken, user.FullName, role);
    }

    public async Task<LoginResponse?> RotateAsync(string rawRefresh, string? deviceName, string? deviceId, CancellationToken cancellationToken)
    {
        var hash = RefreshTokens.Hash(rawRefresh);
        var session = await db.RefreshSessions.AsNoTracking().FirstOrDefaultAsync(s => s.TokenHash == hash, cancellationToken);
        if (session is null) return null;

        var now = DateTime.UtcNow;

        if (session.RevokedAt is not null)
        {
            var supersededByRotation = session.ReplacedByHash is not null;
            var pastGrace = session.RevokedAt <= now.AddSeconds(-ReuseGraceSeconds);
            if (supersededByRotation && pastGrace)
                await db.RefreshSessions
                    .Where(s => s.UserId == session.UserId && s.RevokedAt == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), cancellationToken);
            return null;
        }

        if (session.ExpiresAt <= now) return null;

        var user = await UsersWithGraph().FirstOrDefaultAsync(u => u.Id == session.UserId, cancellationToken);
        if (user is null || !user.IsActive) return null;

        var newRaw = RefreshTokens.Generate();
        var newHash = RefreshTokens.Hash(newRaw);

        var claimed = await db.RefreshSessions
            .Where(s => s.Id == session.Id && s.RevokedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.RevokedAt, now)
                .SetProperty(x => x.ReplacedByHash, newHash), cancellationToken);
        if (claimed == 0) return null;

        var (accessToken, role) = await BuildAccessAsync(user, cancellationToken);
        AddSession(user.Id, deviceName ?? session.DeviceName, deviceId ?? session.DeviceId, newRaw, newHash, now, session.FamilyCreatedAt);
        await db.SaveChangesAsync(cancellationToken);
        return new LoginResponse(accessToken, newRaw, user.FullName, role);
    }

    private string CreateSession(long userId, string? deviceName, string? deviceId, DateTime now, out string hash)
    {
        var raw = RefreshTokens.Generate();
        hash = RefreshTokens.Hash(raw);
        AddSession(userId, deviceName, deviceId, raw, hash, now, now);
        return raw;
    }

    private void AddSession(long userId, string? deviceName, string? deviceId, string raw, string hash, DateTime now, DateTime familyCreatedAt)
    {
        var absolute = familyCreatedAt.AddDays(AbsoluteLifetimeDays);
        var expires = now.AddDays(RefreshLifetimeDays);
        db.RefreshSessions.Add(new RefreshSession
        {
            UserId = userId,
            TokenHash = hash,
            DeviceId = string.IsNullOrWhiteSpace(deviceId) ? null : deviceId.Trim(),
            DeviceName = string.IsNullOrWhiteSpace(deviceName) ? null : deviceName.Trim(),
            Client = currentUser.Client,
            CreatedAt = now,
            FamilyCreatedAt = familyCreatedAt,
            LastUsedAt = now,
            ExpiresAt = expires < absolute ? expires : absolute
        });
    }

    private async Task<(string Token, string Role)> BuildAccessAsync(User user, CancellationToken cancellationToken)
    {
        var roles = user.UserRoles
            .Select(ur => ur.Role)
            .Where(role => role.IsActive)
            .OrderByDescending(role => role.Priority)
            .ToList();
        if (roles.Count == 0)
            throw new ForbiddenException("User has no active role.");
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
            RoleAuthorizationStamp.Create(roles),
            businessId, branchIds, user.DefaultBranchId);

        return (token, roleNames.FirstOrDefault() ?? "");
    }
}
