using Cartex.Application.Common.Interfaces;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Persistence;
using Cartex.Shared.Models.OfflineCache;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.OfflineCache.Commands;

public sealed record ClaimOfflineCacheCommand(
    string DeviceId,
    string DeviceName,
    long WarehouseId) : ICommand<OfflineLeaseGrantDto>;

public sealed class ClaimOfflineCacheCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IAuditService audit)
    : IRequestHandler<ClaimOfflineCacheCommand, OfflineLeaseGrantDto>
{
    public async Task<OfflineLeaseGrantDto> Handle(ClaimOfflineCacheCommand request, CancellationToken cancellationToken)
    {
        var businessId = currentUser.BusinessId
            ?? throw new UnauthorizedAccessException("Business context is missing.");
        var warehouse = await db.Warehouses
            .Where(x => x.Id == request.WarehouseId)
            .Select(x => new { x.Id, x.BranchId, x.Branch.BusinessId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Ombor topilmadi.", "warehouse_not_found");
        if (warehouse.BusinessId != businessId)
            throw new ForbiddenException("Bu omborga ruxsat yo'q.");

        // Serializes contenders before reading the partial-unique active row.
        await db.Businesses
            .FromSqlInterpolated($"SELECT * FROM businesses WHERE id = {businessId} FOR UPDATE")
            .Select(x => x.Id)
            .SingleAsync(cancellationToken);

        var active = await db.OfflineAuthorityLeases
            .FirstOrDefaultAsync(x => x.BusinessId == businessId && x.RevokedAt == null, cancellationToken);
        var now = DateTime.UtcNow;
        var token = OfflineLeaseSecurity.NewToken();
        if (active is not null)
        {
            if (!string.Equals(active.DeviceId, request.DeviceId, StringComparison.Ordinal))
                throw new ConflictException(
                    $"Oflayn savdo vakolati allaqachon \"{active.DeviceName}\" qurilmasiga berilgan. Avval uni bekor qiling.",
                    "offline_authority_already_claimed");
            if (active.WarehouseId != warehouse.Id)
                throw new ConflictException(
                    "Oflayn omborni almashtirishdan oldin amaldagi vakolatni bekor qiling.",
                    "offline_warehouse_change_requires_release");

            // Same physical device may recover a lost secure token. Rotating the
            // secret invalidates any stale process without discarding its sequence.
            active.TokenHash = OfflineLeaseSecurity.Hash(token);
            active.DeviceName = request.DeviceName.Trim();
            active.LastHeartbeatAt = now;
            active.Version++;
        }
        else
        {
            var epoch = await db.OfflineAuthorityLeases
                .Where(x => x.BusinessId == businessId)
                .Select(x => (long?)x.Epoch)
                .MaxAsync(cancellationToken) ?? 0;
            active = new OfflineAuthorityLease
            {
                BusinessId = businessId,
                BranchId = warehouse.BranchId,
                WarehouseId = warehouse.Id,
                DeviceId = request.DeviceId.Trim(),
                DeviceName = request.DeviceName.Trim(),
                TokenHash = OfflineLeaseSecurity.Hash(token),
                Epoch = epoch + 1,
                ClaimedAt = now,
                LastHeartbeatAt = now
            };
            db.OfflineAuthorityLeases.Add(active);
        }

        await db.SaveChangesAsync(cancellationToken);
        audit.SetOutcome("offline.authority_claimed", "offline_authority_leases", active.Id, new
        {
            active.DeviceId,
            active.DeviceName,
            active.WarehouseId,
            active.Epoch
        }, "Oflayn savdo vakolati qurilmaga biriktirildi", active.BranchId);

        return new OfflineLeaseGrantDto(active.Id, active.WarehouseId, active.Epoch, token,
            active.ClaimedAt, active.LastHeartbeatAt, active.LastAcceptedSequence);
    }
}

public sealed class ClaimOfflineCacheCommandValidator : AbstractValidator<ClaimOfflineCacheCommand>
{
    public ClaimOfflineCacheCommandValidator()
    {
        RuleFor(x => x.DeviceId).NotEmpty().MaximumLength(64);
        RuleFor(x => x.DeviceName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.WarehouseId).GreaterThan(0);
    }
}
