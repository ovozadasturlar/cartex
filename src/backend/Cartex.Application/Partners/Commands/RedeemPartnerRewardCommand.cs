using System.Text.Json;
using Cartex.Application.Common.Documents;
using Cartex.Application.Common.Finance;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Inventory;
using Cartex.Application.Common.Measurement;
using Cartex.Application.Common.Shifts;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Persistence.Services;
using Cartex.Shared.Models.Partners;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Partners.Commands;

public sealed record RedeemPartnerRewardCommand(
    long PartnerId,
    PartnerRewardMode Mode,
    decimal Amount,
    long? BranchId = null,
    long? WarehouseId = null,
    long? ProductVariantId = null,
    decimal? ProductQuantity = null,
    DateOnly? BusinessDate = null,
    string? Note = null,
    string? IdempotencyKey = null) : ICommand<PartnerRedemptionCreatedDto>;

public sealed class RedeemPartnerRewardCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ILedgerService ledger,
    IStockAllocator stockAllocator,
    IQuantityPolicyService quantityPolicy,
    IShiftLock shiftLock,
    IAuditService audit,
    InventoryReasonState inventoryReason) : IRequestHandler<RedeemPartnerRewardCommand, PartnerRedemptionCreatedDto>
{
    public async Task<PartnerRedemptionCreatedDto> Handle(
        RedeemPartnerRewardCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(AppPermissions.PartnerRewards.Redeem))
            throw new ForbiddenException("Hamkor mukofotini berishga ruxsat yo'q.");
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");
        var businessId = currentUser.BusinessId ?? throw new BusinessRuleException("Business not found.");
        var branchId = request.BranchId ?? currentUser.DefaultBranchId
            ?? throw new BusinessRuleException("Filial tanlanishi kerak.", "branch_required");
        if (!currentUser.CanAccessAllBranches && !currentUser.BranchIds.Contains(branchId))
            throw new ForbiddenException("Bu filialga ruxsat yo'q.");
        if (!await db.Branches.AnyAsync(x => x.Id == branchId && x.BusinessId == businessId, cancellationToken))
            throw new NotFoundException("Branch not found.");

        var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? null
            : request.IdempotencyKey.Trim();
        if (idempotencyKey is not null)
        {
            var existing = await db.PartnerRedemptionDocuments.AsNoTracking()
                .Where(x => x.BranchId == branchId && x.IdempotencyKey == idempotencyKey)
                .Select(x => new PartnerRedemptionCreatedDto(x.Id, x.DocumentNumber, x.Amount))
                .FirstOrDefaultAsync(cancellationToken);
            if (existing is not null) return existing;
        }

        // Locking the profile serializes two concurrent redemptions for the same partner.
        var partner = await db.PartnerProfiles
            .FromSqlInterpolated($"SELECT * FROM partner_profiles WHERE id = {request.PartnerId} FOR UPDATE")
            .Include(x => x.Party).ThenInclude(x => x.CustomerProfile)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Partner not found.", "partner_not_found");
        if (partner.Party.BusinessId != businessId || !partner.IsEnabled)
            throw new NotFoundException("Partner not found.", "partner_not_found");

        var now = DateTime.UtcNow;
        var programBalances = await db.PartnerRewardEntries
            .Where(x => x.PartnerProfileId == partner.Id && x.Mode == request.Mode
                        && (x.State == PartnerRewardState.Earned
                            || x.State == PartnerRewardState.Reversed
                            || x.State == PartnerRewardState.Redeemed
                            || (x.State == PartnerRewardState.Pending && x.AvailableAt <= now)))
            .GroupBy(x => x.PartnerProgramId)
            .Select(x => new { ProgramId = x.Key, Balance = x.Sum(e => e.Amount) })
            .Where(x => x.Balance > 0)
            .OrderBy(x => x.ProgramId)
            .ToListAsync(cancellationToken);
        var available = programBalances.Sum(x => x.Balance);
        if (request.Amount > available)
            throw new BusinessRuleException(
                $"Yetarli mukofot yo'q. Mavjud: {available:0.####}.", "partner_reward_insufficient");

        var businessDate = request.BusinessDate ?? DateOnly.FromDateTime(now);
        var document = new PartnerRedemptionDocument
        {
            BranchId = branchId,
            PartnerProfileId = partner.Id,
            UserId = userId,
            DocumentNumber = await DocumentNumbers.NextAsync(db, "RWD", businessDate, cancellationToken),
            BusinessDate = businessDate,
            Mode = request.Mode,
            Amount = request.Amount,
            ProductVariantId = request.ProductVariantId,
            ProductQuantity = request.ProductQuantity,
            Note = NormalizeOptional(request.Note),
            IdempotencyKey = idempotencyKey
        };
        db.PartnerRedemptionDocuments.Add(document);
        await db.SaveChangesAsync(cancellationToken);

        var remaining = request.Amount;
        foreach (var balance in programBalances)
        {
            var take = Math.Min(balance.Balance, remaining);
            if (take <= 0) continue;
            document.Entries.Add(new PartnerRewardEntry
            {
                BranchId = branchId,
                PartnerProfileId = partner.Id,
                PartnerProgramId = balance.ProgramId,
                PartnerRedemptionDocumentId = document.Id,
                Mode = request.Mode,
                State = PartnerRewardState.Redeemed,
                Amount = -take,
                AvailableAt = now,
                DetailsJson = JsonSerializer.Serialize(new
                    { document.DocumentNumber, request.ProductVariantId, request.ProductQuantity })
            });
            remaining -= take;
            if (remaining <= 0) break;
        }

        await ApplyPayoutAsync(document, partner, request, userId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        audit.SetOutcome("partner.reward_redeemed", "partner_redemption_documents", document.Id, new
        {
            document.DocumentNumber,
            document.PartnerProfileId,
            document.Mode,
            document.Amount,
            document.ProductVariantId,
            document.ProductQuantity,
            document.BranchId
        }, "Hamkor mukofoti berildi", branchId);
        return new PartnerRedemptionCreatedDto(document.Id, document.DocumentNumber, document.Amount);
    }

    private async Task ApplyPayoutAsync(
        PartnerRedemptionDocument document,
        PartnerProfile partner,
        RedeemPartnerRewardCommand request,
        long userId,
        CancellationToken cancellationToken)
    {
        switch (request.Mode)
        {
            case PartnerRewardMode.Cash:
            {
                var shift = await shiftLock.OpenAsync(userId, document.BranchId, cancellationToken)
                    ?? throw new BusinessRuleException("Naqd mukofot uchun ochiq smena kerak.", "open_shift_required");
                var cash = await CashPayout.AccountAsync(ledger, document.BranchId, document.Amount, cancellationToken);
                var transaction = await ledger.PostAsync(OperationType.PartnerRewardCash, document.Amount,
                    cash, null, userId, cancellationToken, shift.Id);
                transaction.PartnerRedemptionDocumentId = document.Id;
                transaction.Description = $"Hamkor mukofoti {document.DocumentNumber}";
                break;
            }
            case PartnerRewardMode.Bonus:
            {
                var customerId = partner.Party.CustomerProfile?.Id
                    ?? throw new BusinessRuleException(
                        "Bonus berish uchun hamkor mijoz profiliga ham ega bo'lishi kerak.", "partner_customer_profile_required");
                var bonus = await ledger.CustomerAccountAsync(customerId, AccountType.Bonus, cancellationToken);
                var transaction = await ledger.PostAsync(OperationType.PartnerRewardBonus, document.Amount,
                    null, bonus, userId, cancellationToken);
                transaction.PartnerRedemptionDocumentId = document.Id;
                transaction.Description = $"Hamkor bonusi {document.DocumentNumber}";
                break;
            }
            case PartnerRewardMode.Product:
            {
                var warehouseId = request.WarehouseId
                    ?? throw new BusinessRuleException("Sovg'a mahsuloti uchun ombor tanlanishi kerak.", "warehouse_required");
                var variantId = request.ProductVariantId
                    ?? throw new BusinessRuleException("Sovg'a mahsuloti tanlanishi kerak.", "product_required");
                var quantity = request.ProductQuantity
                    ?? throw new BusinessRuleException("Sovg'a miqdori kiritilishi kerak.", "product_quantity_required");
                var warehouse = await db.Warehouses.AsNoTracking().FirstOrDefaultAsync(x =>
                    x.Id == warehouseId && x.BranchId == document.BranchId, cancellationToken)
                    ?? throw new BusinessRuleException("Ombor mukofot filiali bilan mos emas.", "warehouse_branch_mismatch");
                await quantityPolicy.ValidateAsync([(variantId, quantity)], cancellationToken);
                inventoryReason.Declare(new(InventoryMovementKind.PartnerReward, "PartnerRedemptionDocument",
                    document.Id, InventoryLocation.External(partner.PartyId)));
                await stockAllocator.PreloadAsync(warehouse.Id, [variantId], cancellationToken);
                await stockAllocator.AllocateAsync(
                    warehouse.Id, variantId, quantity, allowInsufficientStock: false, cancellationToken);
                stockAllocator.Apply();
                break;
            }
            case PartnerRewardMode.Points:
                // Points can represent a configured external prize. The immutable document
                // records who approved and what was handed over in Note.
                break;
            default:
                throw new BusinessRuleException("Mukofot turi qo'llab-quvvatlanmaydi.", "unsupported_reward_mode");
        }
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class RedeemPartnerRewardCommandValidator : AbstractValidator<RedeemPartnerRewardCommand>
{
    public RedeemPartnerRewardCommandValidator()
    {
        RuleFor(x => x.PartnerId).GreaterThan(0);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.ProductQuantity).GreaterThan(0).When(x => x.ProductQuantity.HasValue);
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.IdempotencyKey).MaximumLength(128);
        RuleFor(x => x).Must(x => x.Mode != PartnerRewardMode.Product
                                  || (x.WarehouseId.HasValue && x.ProductVariantId.HasValue
                                      && x.ProductQuantity is > 0))
            .WithMessage("Sovg'a mahsuloti, ombor va miqdor tanlanishi kerak.");
    }
}
