using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

using Unit = Cartex.Application.Common.Messaging.Unit;

namespace Cartex.Application.OfflineCache.Commands;

public sealed record ReleaseOfflineCacheCommand(
    long? LeaseId = null,
    string? LeaseToken = null,
    bool Force = false,
    string? Reason = null) : ICommand<Unit>;

public sealed class ReleaseOfflineCacheCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IAuditService audit)
    : IRequestHandler<ReleaseOfflineCacheCommand, Unit>
{
    public async Task<Unit> Handle(ReleaseOfflineCacheCommand request, CancellationToken cancellationToken)
    {
        var businessId = currentUser.BusinessId
            ?? throw new UnauthorizedAccessException("Business context is missing.");
        await db.Businesses
            .FromSqlInterpolated($"SELECT * FROM businesses WHERE id = {businessId} FOR UPDATE")
            .Select(x => x.Id)
            .SingleAsync(cancellationToken);

        var active = await db.OfflineAuthorityLeases
            .FirstOrDefaultAsync(x => x.BusinessId == businessId && x.RevokedAt == null, cancellationToken)
            ?? throw new NotFoundException("Faol oflayn vakolat topilmadi.", "offline_lease_not_found");
        if (request.LeaseId is not null && active.Id != request.LeaseId)
            throw new ConflictException("Oflayn vakolat almashtirilgan.", "offline_lease_mismatch");

        if (request.Force)
        {
            if (!currentUser.HasPermission(AppPermissions.Devices.Revoke))
                throw new ForbiddenException("Oflayn vakolatni majburan bekor qilishga ruxsat yo'q.");
        }
        else
        {
            if (!string.Equals(currentUser.DeviceId, active.DeviceId, StringComparison.Ordinal))
                throw new ForbiddenException("Faqat vakolat egasi yoki administrator uni bekor qilishi mumkin.");
            if (!OfflineLeaseSecurity.TokenMatches(request.LeaseToken ?? string.Empty, active.TokenHash))
                throw new ForbiddenException("Oflayn vakolat kaliti noto'g'ri.", "offline_lease_token_invalid");
        }

        active.RevokedAt = DateTime.UtcNow;
        active.RevokedByUserId = currentUser.UserId;
        active.RevokeReason = string.IsNullOrWhiteSpace(request.Reason)
            ? request.Force ? "Administrator tomonidan majburan bekor qilindi" : "Qurilma tomonidan bekor qilindi"
            : request.Reason.Trim();
        active.Version++;
        await db.SaveChangesAsync(cancellationToken);

        audit.SetOutcome("offline.authority_released", "offline_authority_leases", active.Id, new
        {
            active.DeviceId,
            active.DeviceName,
            active.WarehouseId,
            active.Epoch,
            request.Force,
            active.RevokeReason,
            active.LastAcceptedSequence,
            active.LastReportedPendingCount,
            active.LastSyncAt
        }, "Oflayn savdo vakolati bekor qilindi", active.BranchId);
        return Unit.Value;
    }
}

public sealed class ReleaseOfflineCacheCommandValidator : AbstractValidator<ReleaseOfflineCacheCommand>
{
    public ReleaseOfflineCacheCommandValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(500);
        RuleFor(x => x.LeaseToken).MaximumLength(256);
    }
}
