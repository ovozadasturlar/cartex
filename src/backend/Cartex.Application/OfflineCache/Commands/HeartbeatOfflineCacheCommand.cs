using Cartex.Domain.Common;
using Cartex.Persistence;
using Cartex.Shared.Models.OfflineCache;

namespace Cartex.Application.OfflineCache.Commands;

public sealed record HeartbeatOfflineCacheCommand(
    long LeaseId,
    long Epoch,
    string LeaseToken,
    long PendingCount) : ICommand<OfflineHeartbeatDto>;

public sealed class HeartbeatOfflineCacheCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser)
    : IRequestHandler<HeartbeatOfflineCacheCommand, OfflineHeartbeatDto>
{
    public async Task<OfflineHeartbeatDto> Handle(
        HeartbeatOfflineCacheCommand request,
        CancellationToken cancellationToken)
    {
        if (request.PendingCount < 0)
            throw new BusinessRuleException("Kutilayotgan amallar soni noto'g'ri.", "invalid_pending_count");

        var lease = await OfflineLeaseSecurity.RequireActiveAsync(db, currentUser,
            request.LeaseId, request.Epoch, request.LeaseToken, true, cancellationToken);
        var now = DateTime.UtcNow;
        lease.LastHeartbeatAt = now;
        lease.LastReportedPendingCount = request.PendingCount;
        lease.Version++;
        await db.SaveChangesAsync(cancellationToken);
        return new OfflineHeartbeatDto(now, lease.LastAcceptedSequence, true);
    }
}
