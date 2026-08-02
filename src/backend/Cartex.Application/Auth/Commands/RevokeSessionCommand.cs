using Cartex.Application.Common.Messaging;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Common.Exceptions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Auth.Commands;

public record RevokeSessionCommand(long Id, bool ReleaseOffline = false) : IRequest<Unit>;

public sealed class RevokeSessionCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ISettingsService settings,
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

        var offline = await settings.GetAsync<OfflineCacheSettings>(SettingKeys.OfflineCache, cancellationToken);
        var isOfflineHolder = target.DeviceId is not null && target.DeviceId == offline?.DeviceId;
        if (isOfflineHolder && !request.ReleaseOffline)
            throw new BusinessRuleException("Bu qurilmaga offline savdo biriktirilgan. Alohida tasdiq talab qilinadi.");

        if (isOfflineHolder)
        {
            audit.Add("offlineRelease", "settings", null, new { offline!.DeviceId, offline.DeviceName, Source = "sessionRevoke" });
            await settings.SetAsync(SettingKeys.OfflineCache, new OfflineCacheSettings(), cancellationToken);
        }

        var sessions = db.RefreshSessions.Where(s => s.UserId == target.UserId && s.RevokedAt == null);
        sessions = target.DeviceId is not null
            ? sessions.Where(s => s.DeviceId == target.DeviceId)
            : sessions.Where(s => s.DeviceId == null && s.DeviceName == target.DeviceName);
        await sessions
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), cancellationToken);

        audit.Add("sessionRevoke", "refresh_sessions", request.Id, new { target.UserId, target.DeviceId, target.DeviceName, isOfflineHolder });
        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
