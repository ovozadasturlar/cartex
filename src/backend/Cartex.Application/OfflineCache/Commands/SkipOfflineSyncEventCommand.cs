using Cartex.Domain.Common;
using Cartex.Persistence;
using Cartex.Shared.Models.OfflineCache;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.OfflineCache.Commands;

public sealed record SkipOfflineSyncEventCommand(
    long LeaseId,
    long Epoch,
    string LeaseToken,
    OfflineSyncEventRequest Event,
    string? Reason,
    bool ForImport = false) : ICommand<OfflineSyncEventResult>;

public sealed class SkipOfflineSyncEventCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IAuditService audit)
    : IRequestHandler<SkipOfflineSyncEventCommand, OfflineSyncEventResult>
{
    public async Task<OfflineSyncEventResult> Handle(SkipOfflineSyncEventCommand request, CancellationToken cancellationToken)
    {
        var actorId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");
        var row = request.Event;
        if (row.EventId == Guid.Empty || row.Sequence <= 0)
            throw new BusinessRuleException("Oflayn amal identifikatori noto'g'ri.", "invalid_offline_event");
        if (string.IsNullOrWhiteSpace(row.IdempotencyKey) || row.IdempotencyKey.Length > 128)
            throw new BusinessRuleException("Idempotency key noto'g'ri.", "invalid_idempotency_key");

        var rawPayload = row.Payload.GetRawText();
        if (rawPayload.Length > 512 * 1024)
            throw new BusinessRuleException("Oflayn amal hajmi 512 KB dan oshmasligi kerak.", "offline_payload_too_large");
        var payloadHash = OfflineLeaseSecurity.Hash(rawPayload);
        var kind = OfflineEventKinds.Normalize(row.Kind);

        var lease = request.ForImport
            ? await OfflineLeaseSecurity.RequireForImportAsync(db, currentUser,
                request.LeaseId, request.Epoch, request.LeaseToken, true, cancellationToken)
            : await OfflineLeaseSecurity.RequireActiveAsync(db, currentUser,
                request.LeaseId, request.Epoch, request.LeaseToken, true, cancellationToken);

        var existing = await db.OfflineSyncEvents.AsNoTracking()
            .FirstOrDefaultAsync(x => x.EventId == row.EventId, cancellationToken);
        if (existing is not null)
        {
            if (existing.Status == "Applied")
                throw new ConflictException(
                    "Bu amal allaqachon qo'llangan — o'tkazib yuborib bo'lmaydi.",
                    "offline_event_already_applied");
            if (existing.OfflineAuthorityLeaseId != lease.Id
                || existing.Sequence != row.Sequence
                || existing.PayloadHash != payloadHash)
                throw new ConflictException("EventId boshqa mazmun bilan avval ishlatilgan.", "offline_event_conflict");
            return new OfflineSyncEventResult(row.EventId, row.Sequence, "Skipped");
        }

        if (row.Sequence != lease.LastAcceptedSequence + 1)
            throw new ConflictException(
                $"Kutilgan sequence {lease.LastAcceptedSequence + 1}, yuborilgan {row.Sequence}.",
                "offline_sequence_gap");

        var now = DateTime.UtcNow;
        db.OfflineSyncEvents.Add(new Domain.Entities.OfflineSyncEvent
        {
            OfflineAuthorityLeaseId = lease.Id,
            EventId = row.EventId,
            Sequence = row.Sequence,
            Kind = kind,
            IdempotencyKey = row.IdempotencyKey.Trim(),
            PayloadHash = payloadHash,
            ActorUserId = row.ActorUserId ?? actorId,
            Status = "Skipped",
            DeviceOccurredAt = row.OccurredAt.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(row.OccurredAt, DateTimeKind.Utc)
                : row.OccurredAt.ToUniversalTime(),
            ProcessedAt = now
        });
        lease.LastAcceptedSequence = row.Sequence;
        if (!request.ForImport)
            lease.LastHeartbeatAt = now;
        lease.Version++;
        audit.Add("offlineEventSkipped", "offline_sync_events", null,
            new { row.EventId, row.Sequence, Kind = kind, request.Reason });
        await db.SaveChangesAsync(cancellationToken);

        return new OfflineSyncEventResult(row.EventId, row.Sequence, "Skipped");
    }
}

public sealed class SkipOfflineSyncEventCommandValidator : AbstractValidator<SkipOfflineSyncEventCommand>
{
    public SkipOfflineSyncEventCommandValidator()
    {
        RuleFor(x => x.LeaseId).GreaterThan(0);
        RuleFor(x => x.Epoch).GreaterThan(0);
        RuleFor(x => x.LeaseToken).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Event).NotNull();
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}
