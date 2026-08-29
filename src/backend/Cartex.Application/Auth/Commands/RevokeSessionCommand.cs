using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Auth.Commands;

public record RevokeSessionCommand(long Id, bool ReleaseOffline = false) : ICommand<Unit>;

public sealed class RevokeSessionCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IAuditService audit)
    : IRequestHandler<RevokeSessionCommand, Unit>
{
    public async Task<Unit> Handle(RevokeSessionCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");
        var canManageAll = currentUser.HasPermission(AppPermissions.Devices.ViewAll);
        var now = DateTime.UtcNow;
        var target = await db.RefreshSessions
            .Where(s => s.Id == request.Id && (canManageAll || s.UserId == userId) && s.RevokedAt == null)
            .Select(s => new { s.UserId, s.DeviceId, s.DeviceName })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Session not found.");

        var businessId = currentUser.BusinessId
            ?? throw new UnauthorizedAccessException("Business context is missing.");
        await db.Businesses
            .FromSqlInterpolated($"SELECT * FROM businesses WHERE id = {businessId} FOR UPDATE")
            .Select(x => x.Id)
            .SingleAsync(cancellationToken);
        var offline = await db.OfflineAuthorityLeases
            .FirstOrDefaultAsync(x => x.BusinessId == businessId
                && x.RevokedAt == null
                && target.DeviceId != null
                && x.DeviceId == target.DeviceId, cancellationToken);
        var isOfflineHolder = offline is not null;
        if (isOfflineHolder && !request.ReleaseOffline)
            throw new BusinessRuleException("Bu qurilmaga offline savdo biriktirilgan. Alohida tasdiq talab qilinadi.");

        if (isOfflineHolder)
        {
            offline!.RevokedAt = now;
            offline.RevokedByUserId = userId;
            offline.RevokeReason = "Qurilma sessiyasi bekor qilindi";
            offline.Version++;
            audit.Add("offline.authority_released", "offline_authority_leases", offline.Id,
                new { offline.DeviceId, offline.DeviceName, offline.WarehouseId, offline.Epoch, Source = "sessionRevoke" });
        }

        var sessions = db.RefreshSessions.Where(s => s.UserId == target.UserId && s.RevokedAt == null);
        sessions = target.DeviceId is not null
            ? sessions.Where(s => s.DeviceId == target.DeviceId)
            : sessions.Where(s => s.DeviceId == null && s.DeviceName == target.DeviceName);
        await sessions
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), cancellationToken);

        audit.SetOutcome("session.revoked", "refresh_sessions", request.Id,
            new { target.UserId, target.DeviceId, target.DeviceName, isOfflineHolder },
            "Qurilma sessiyasi bekor qilindi");
        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
