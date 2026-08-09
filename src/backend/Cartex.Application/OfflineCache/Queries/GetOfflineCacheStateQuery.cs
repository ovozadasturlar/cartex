using Cartex.Domain.Common;
using Cartex.Persistence;
using Cartex.Shared.Models.OfflineCache;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.OfflineCache.Queries;

public sealed record GetOfflineCacheStateQuery : IRequest<OfflineCacheStateDto>;

public sealed class GetOfflineCacheStateQueryHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetOfflineCacheStateQuery, OfflineCacheStateDto>
{
    public async Task<OfflineCacheStateDto> Handle(GetOfflineCacheStateQuery request, CancellationToken cancellationToken)
    {
        var businessId = currentUser.BusinessId
            ?? throw new UnauthorizedAccessException("Business context is missing.");
        var row = await db.OfflineAuthorityLeases.AsNoTracking()
            .Where(x => x.BusinessId == businessId && x.RevokedAt == null)
            .Select(x => new
            {
                x.Id,
                x.DeviceId,
                x.DeviceName,
                x.ClaimedAt,
                x.WarehouseId,
                WarehouseName = x.Warehouse.Name,
                x.Epoch,
                x.LastHeartbeatAt,
                x.LastSyncAt,
                x.LastAcceptedSequence,
                x.LastReportedPendingCount
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null) return new OfflineCacheStateDto(null, null, null);

        return new OfflineCacheStateDto(row.DeviceId, row.DeviceName, row.ClaimedAt,
            row.Id, row.WarehouseId, row.WarehouseName, row.Epoch, row.LastHeartbeatAt,
            row.LastSyncAt, row.LastAcceptedSequence,
            row.LastReportedPendingCount,
            string.Equals(currentUser.DeviceId, row.DeviceId, StringComparison.Ordinal),
            row.LastHeartbeatAt < DateTime.UtcNow.AddSeconds(-60));
    }
}
