using System.Text.Json;
using Cartex.Application.Common.Finance;
using Cartex.Application.Common.Participants;
using Cartex.Application.CustomerPayments.Commands;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Supplies.Commands;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Shared.Models.Customers;
using Cartex.Shared.Models.OfflineCache;
using Cartex.Shared.Models.Sales;
using Cartex.Shared.Models.Supplies;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.OfflineCache.Commands;

public sealed record ProcessOfflineSyncBatch(
    long LeaseId,
    long Epoch,
    string LeaseToken,
    IReadOnlyList<OfflineSyncEventRequest> Events) : IRequest<OfflineSyncBatchResult>;

public sealed class ProcessOfflineSyncBatchHandler(
    ISender sender)
    : IRequestHandler<ProcessOfflineSyncBatch, OfflineSyncBatchResult>
{
    public async Task<OfflineSyncBatchResult> Handle(ProcessOfflineSyncBatch request, CancellationToken cancellationToken)
    {
        var heartbeat = await sender.Send(new HeartbeatOfflineCacheCommand(
            request.LeaseId, request.Epoch, request.LeaseToken, request.Events.Count), cancellationToken);
        var lastAccepted = heartbeat.LastAcceptedSequence;
        var results = new List<OfflineSyncEventResult>(request.Events.Count);

        for (var index = 0; index < request.Events.Count; index++)
        {
            var row = request.Events[index];
            try
            {
                var applied = await sender.Send(new ApplyOfflineSyncEventCommand(
                    request.LeaseId, request.Epoch, request.LeaseToken, row), cancellationToken);
                results.Add(applied);
                lastAccepted = Math.Max(lastAccepted, applied.Sequence);
            }
            catch (DomainException ex)
            {
                results.Add(new OfflineSyncEventResult(row.EventId, row.Sequence, "Rejected",
                    ErrorCode: ex.Code, Error: ex.Message));
                AddDeferred(index + 1);
                break;
            }
            catch (ValidationException ex)
            {
                results.Add(new OfflineSyncEventResult(row.EventId, row.Sequence, "Rejected",
                    ErrorCode: "validation_error", Error: ex.Errors.FirstOrDefault()?.ErrorMessage ?? ex.Message));
                AddDeferred(index + 1);
                break;
            }
        }

        return new OfflineSyncBatchResult(request.LeaseId, request.Epoch, lastAccepted,
            DateTime.UtcNow, results);

        void AddDeferred(int start)
        {
            for (var i = start; i < request.Events.Count; i++)
            {
                var deferred = request.Events[i];
                results.Add(new OfflineSyncEventResult(deferred.EventId, deferred.Sequence, "Deferred",
                    ErrorCode: "prior_event_rejected",
                    Error: "Oldingi amal tuzatilmaguncha navbat davom ettirilmaydi."));
            }
        }
    }
}

public sealed class ProcessOfflineSyncBatchValidator : AbstractValidator<ProcessOfflineSyncBatch>
{
    public ProcessOfflineSyncBatchValidator()
    {
        RuleFor(x => x.LeaseId).GreaterThan(0);
        RuleFor(x => x.Epoch).GreaterThan(0);
        RuleFor(x => x.LeaseToken).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Events).NotNull().Must(x => x.Count <= 100)
            .WithMessage("Bir batchda ko'pi bilan 100 ta amal yuboriladi.");
        RuleFor(x => x.Events).Must(IsStrictlyOrdered)
            .WithMessage("Oflayn amallar sequence bo'yicha qat'iy o'suvchi tartibda bo'lishi kerak.");
    }

    private static bool IsStrictlyOrdered(IReadOnlyList<OfflineSyncEventRequest> rows)
    {
        long previous = 0;
        foreach (var row in rows)
        {
            if (row.Sequence <= previous) return false;
            previous = row.Sequence;
        }
        return true;
    }
}

public sealed record ApplyOfflineSyncEventCommand(
    long LeaseId,
    long Epoch,
    string LeaseToken,
    OfflineSyncEventRequest Event,
    bool ForImport = false) : ICommand<OfflineSyncEventResult>;

public sealed class ApplyOfflineSyncEventCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ICurrencyService currency,
    ISender sender)
    : IRequestHandler<ApplyOfflineSyncEventCommand, OfflineSyncEventResult>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<OfflineSyncEventResult> Handle(
        ApplyOfflineSyncEventCommand request,
        CancellationToken cancellationToken)
    {
        var actorId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");
        var row = request.Event;
        var effectiveActorId = row.ActorUserId ?? actorId;
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
        if (effectiveActorId != actorId)
            await EnsureReplayActorAsync(effectiveActorId, lease.BusinessId, lease.BranchId,
                OfflineEventKinds.ReplayActorPermissions(kind), cancellationToken);
        var existing = await db.OfflineSyncEvents.AsNoTracking()
            .FirstOrDefaultAsync(x => x.EventId == row.EventId, cancellationToken);
        if (existing is not null)
        {
            if (existing.OfflineAuthorityLeaseId != lease.Id
                || existing.Sequence != row.Sequence
                || existing.PayloadHash != payloadHash
                || !string.Equals(existing.Kind, kind, StringComparison.Ordinal)
                || !string.Equals(existing.IdempotencyKey, row.IdempotencyKey, StringComparison.Ordinal))
                throw new ConflictException("EventId boshqa mazmun bilan avval ishlatilgan.", "offline_event_conflict");

            if (!request.ForImport)
            {
                lease.LastHeartbeatAt = DateTime.UtcNow;
                lease.Version++;
                await db.SaveChangesAsync(cancellationToken);
            }
            return new OfflineSyncEventResult(row.EventId, row.Sequence, "AlreadyApplied",
                existing.ResultEntityId, existing.ResultCode);
        }

        if (row.Sequence != lease.LastAcceptedSequence + 1)
            throw new ConflictException(
                $"Kutilgan sequence {lease.LastAcceptedSequence + 1}, yuborilgan {row.Sequence}.",
                "offline_sequence_gap");
        if (await db.OfflineSyncEvents.AnyAsync(x =>
                x.OfflineAuthorityLeaseId == lease.Id && x.Sequence == row.Sequence, cancellationToken))
            throw new ConflictException("Bu sequence avval ishlatilgan.", "offline_sequence_conflict");

        var (entityId, resultCode) = kind switch
        {
            OfflineEventKinds.Sale => await ApplySaleAsync(
                rawPayload, row.IdempotencyKey, effectiveActorId, cancellationToken),
            OfflineEventKinds.Payment => await ApplyCustomerPaymentAsync(
                rawPayload, row.IdempotencyKey, effectiveActorId, cancellationToken),
            OfflineEventKinds.Supply => await ApplySupplyAsync(
                rawPayload, row.IdempotencyKey, effectiveActorId, cancellationToken),
            _ => throw new BusinessRuleException($"Oflayn amal turi qo'llanmaydi: {row.Kind}", "offline_event_kind_unsupported")
        };

        var now = DateTime.UtcNow;
        db.OfflineSyncEvents.Add(new Domain.Entities.OfflineSyncEvent
        {
            OfflineAuthorityLeaseId = lease.Id,
            EventId = row.EventId,
            Sequence = row.Sequence,
            Kind = kind,
            IdempotencyKey = row.IdempotencyKey.Trim(),
            PayloadHash = payloadHash,
            ActorUserId = effectiveActorId,
            Status = "Applied",
            ResultEntityId = entityId,
            ResultCode = resultCode,
            DeviceOccurredAt = row.OccurredAt.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(row.OccurredAt, DateTimeKind.Utc)
                : row.OccurredAt.ToUniversalTime(),
            ProcessedAt = now
        });
        lease.LastAcceptedSequence = row.Sequence;
        // Import o'lik qurilma nomidan — heartbeat yangilanmaydi, aks holda split-brain
        // qo'riqchisi qurilmani tirik deb o'ylaydi.
        if (!request.ForImport)
            lease.LastHeartbeatAt = now;
        lease.LastSyncAt = now;
        lease.Version++;
        await db.SaveChangesAsync(cancellationToken);

        return new OfflineSyncEventResult(row.EventId, row.Sequence, "Applied", entityId, resultCode);
    }

    private async Task<(long Id, string Code)> ApplySaleAsync(
        string payload,
        string idempotencyKey,
        long actorUserId,
        CancellationToken cancellationToken)
    {
        var dto = Deserialize<CreateSaleRequest>(payload);
        var payments = dto.Payments?.Select(x => new SalePaymentDto(
            ParsePaymentMethod(x.Method), x.Currency, x.Amount)).ToList();
        var participants = dto.Participants?.Select(x =>
            new ParticipantInput(x.RoleDefinitionId, x.PartyId)).ToList();
        // OFF-10: kassir ko'rgan narx va kiritilgan chegirma navbatdagi savat kabi
        // oldindan ruxsatlangan — sinxronlashayotgan foydalanuvchidan qayta so'ralmaydi.
        var preauthorizedPrices = dto.Items
            .Where(x => x.UnitPrice is not null)
            .GroupBy(x => x.VariantId)
            .Where(x => x.Select(i => i.UnitPrice!.Value).Distinct().Count() == 1)
            .ToDictionary(x => x.Key, x => x.First().UnitPrice!.Value);
        var result = await sender.Send(new CreateSaleCommand(
            WarehouseId: dto.WarehouseId,
            CustomerId: dto.CustomerId,
            PaidCash: dto.PaidCash,
            PaidCard: dto.PaidCard,
            PaidBonus: dto.PaidBonus,
            Items: dto.Items.Select(x => new CreateSaleItemDto(
                x.VariantId, x.Quantity, x.UnitPrice, x.PrepackId)).ToList())
        {
            DiscountAmount = dto.DiscountAmount,
            Payments = payments,
            DebtCurrency = dto.DebtCurrency,
            DebtDueDate = dto.DebtDueDate,
            IdempotencyKey = idempotencyKey,
            ApplyAutoDiscount = dto.ApplyAutoDiscount,
            CreditAmount = dto.CreditAmount,
            UseCustomerAdvance = dto.UseCustomerAdvance,
            Participants = participants,
            FromOfflineSync = true,
            OfflineActorUserId = actorUserId,
            PreauthorizedPrices = preauthorizedPrices.Count > 0 ? preauthorizedPrices : null,
            PreauthorizedDiscountAmount = dto.DiscountAmount
        }, cancellationToken);
        return (result.SaleId, result.ReceiptToken);
    }

    private async Task<(long Id, string Code)> ApplyCustomerPaymentAsync(
        string payload,
        string idempotencyKey,
        long actorUserId,
        CancellationToken cancellationToken)
    {
        var dto = Deserialize<CreateCustomerPaymentRequest>(payload);
        // OFF-20: oflayn faqat oddiy to'lov — taqsimotni server QARZ-03 bo'yicha o'zi qiladi.
        if (dto.Allocations is { Count: > 0 } || !dto.AutoAllocateDebt)
            throw new BusinessRuleException(
                "Oflayn to'lovda qo'lda taqsimot qo'llanmaydi.",
                "offline_payment_allocations_unsupported");
        var result = await sender.Send(new CreateCustomerPaymentCommand(
            dto.CustomerId,
            dto.BranchId,
            dto.Tenders.Select(x => new CustomerPaymentTenderInput(
                ParsePaymentMethod(x.Method), x.Currency, x.Amount)).ToList(),
            null,
            true,
            dto.BusinessDate,
            dto.Note,
            idempotencyKey)
        {
            FromOfflineSync = true,
            OfflineActorUserId = actorUserId
        }, cancellationToken);
        return (result.Id, result.DocumentNumber);
    }

    private async Task<(long Id, string Code)> ApplySupplyAsync(
        string payload,
        string idempotencyKey,
        long actorUserId,
        CancellationToken cancellationToken)
    {
        var dto = Deserialize<CreateSupplyRequest>(payload);
        // OFF-30: oflayn kirim faqat qarzga va faqat baza valyutada.
        if (dto.PaidCash != 0 || dto.PaidCard != 0)
            throw new BusinessRuleException(
                "Oflayn kirim faqat qarzga bo'ladi — to'lov onlaynda qilinadi.",
                "offline_supply_payment_unsupported");
        var baseCode = await currency.BaseAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(dto.Currency)
            && !string.Equals(dto.Currency.Trim(), baseCode, StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException(
                "Oflayn kirim faqat baza valyutada bo'ladi.",
                "offline_supply_currency_unsupported");
        var supplyId = await sender.Send(new CreateSupplyCommand(
            dto.SupplierId,
            dto.WarehouseId,
            dto.SupplyDate,
            dto.Items.Select(x => new CreateSupplyItemDto(
                x.VariantId, x.Quantity, x.PurchasePrice, x.ExpiredAt,
                x.UnitId, x.SellingPrice, x.PackId, ParsePriceBasis(x.PriceBasis))).ToList())
        {
            IdempotencyKey = idempotencyKey,
            FromOfflineSync = true,
            OfflineActorUserId = actorUserId
        }, cancellationToken);
        return (supplyId, supplyId.ToString());
    }

    private static T Deserialize<T>(string payload) where T : class =>
        JsonSerializer.Deserialize<T>(payload, JsonOptions)
        ?? throw new BusinessRuleException("Oflayn amal ma'lumoti bo'sh.", "offline_payload_invalid");

    private static PaymentMethod ParsePaymentMethod(string value) =>
        Enum.TryParse<PaymentMethod>(value, true, out var method)
            ? method
            : throw new BusinessRuleException($"To'lov turi noto'g'ri: {value}", "invalid_payment_method");

    private static SupplyPriceBasis ParsePriceBasis(string value) =>
        Enum.TryParse<SupplyPriceBasis>(value, true, out var basis)
            ? basis
            : throw new BusinessRuleException($"Kirim narx asosi noto'g'ri: {value}", "invalid_price_basis");

    private async Task EnsureReplayActorAsync(
        long actorUserId,
        long businessId,
        long branchId,
        string[] permissions,
        CancellationToken cancellationToken)
    {
        var valid = await db.Users.AsNoTracking().AnyAsync(x =>
            x.Id == actorUserId
            && x.IsActive
            && ((x.DefaultBranch != null && x.DefaultBranch.BusinessId == businessId)
                || x.UserBranches.Any(ub => ub.Branch.BusinessId == businessId))
            && (x.UserRoles.Any(ur => ur.Role.IsActive && ur.Role.AccessAll)
                || x.UserRoles.Any(ur => ur.Role.IsActive && ur.Role.RolePermissions.Any(rp =>
                    rp.Permission.IsEnabled
                    && permissions.Contains(rp.Permission.Name))))
            && (x.UserRoles.Any(ur => ur.Role.IsActive && ur.Role.AccessAll)
                || x.DefaultBranchId == branchId
                || x.UserBranches.Any(ub => ub.BranchId == branchId)), cancellationToken);
        if (!valid)
            throw new ForbiddenException(
                "Oflayn amalni yaratgan foydalanuvchi faol emas yoki savdo ruxsati bekor qilingan.",
                "offline_actor_not_authorized");
    }
}
