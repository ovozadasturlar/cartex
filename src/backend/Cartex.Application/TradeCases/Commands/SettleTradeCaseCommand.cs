using System.Security.Cryptography;
using System.Text;
using Cartex.Application.Common.Documents;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Sales.Commands;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Shared.Models.TradeCases;
using Cartex.Application.Common.Participants;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.TradeCases.Commands;

public sealed record TradeCaseSettlementLineInput(long GoodsIssueLineId, decimal Quantity);

public sealed record SettleTradeCaseCommand(
    long TradeCaseId,
    decimal PaidCash,
    decimal PaidCard,
    decimal PaidBonus,
    List<SalePaymentDto>? Payments = null,
    List<TradeCaseSettlementLineInput>? Lines = null,
    decimal DiscountAmount = 0,
    string? DebtCurrency = null,
    DateOnly? DebtDueDate = null,
    bool ApplyAutoDiscount = true,
    bool UseCustomerAdvance = true,
    bool CloseWhenEmpty = true,
    DateOnly? BusinessDate = null,
    string? Note = null,
    string? IdempotencyKey = null,
    int? ExpectedCaseVersion = null) : ICommand<TradeCaseSettlementCreatedDto>;

public sealed class SettleTradeCaseCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ISender sender,
    IAuditService audit) : IRequestHandler<SettleTradeCaseCommand, TradeCaseSettlementCreatedDto>
{
    public async Task<TradeCaseSettlementCreatedDto> Handle(SettleTradeCaseCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");
        if (!currentUser.HasPermission(AppPermissions.TradeCases.Settle))
            throw new ForbiddenException("Loyihani hisob-kitob qilishga ruxsat yo'q.");

        var tradeCase = await db.TradeCases
            .FromSqlInterpolated($"SELECT * FROM trade_cases WHERE id = {request.TradeCaseId} FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Trade case not found.", "trade_case_not_found");

        var idempotencyKey = NormalizeOptional(request.IdempotencyKey);
        if (idempotencyKey is not null)
        {
            var existing = await db.TradeCaseSettlements
                .Where(x => x.BranchId == tradeCase.BranchId && x.IdempotencyKey == idempotencyKey)
                .Select(x => new TradeCaseSettlementCreatedDto(
                    x.Id, x.DocumentNumber, x.SaleId, x.Sale.ReceiptToken, x.Amount,
                    x.TradeCase.Status == TradeCaseStatus.Settled, x.TradeCase.Version))
                .FirstOrDefaultAsync(cancellationToken);
            if (existing is not null) return existing;
        }

        if (tradeCase.Status is TradeCaseStatus.Settled or TradeCaseStatus.Cancelled)
            throw new BusinessRuleException("Loyiha allaqachon yopilgan.", "trade_case_closed");
        if (tradeCase.Workflow != TradeCaseWorkflow.CustodyUntilSettlement)
            throw new BusinessRuleException("Bu loyiha oddiy savdo hujjatlari bilan hisoblanadi.", "custody_workflow_required");
        if (request.ExpectedCaseVersion.HasValue && request.ExpectedCaseVersion != tradeCase.Version)
            throw new ConflictException("Loyiha boshqa qurilmada yangilangan. Ma'lumotni qayta yuklang.", "trade_case_version_conflict");

        var issueLines = await db.GoodsIssueLines
            .Where(x => x.Document.TradeCaseId == tradeCase.Id
                        && x.Document.Status == BusinessDocumentStatus.Posted
                        && x.Quantity > x.ReturnedQuantity + x.SettledQuantity)
            .OrderBy(x => x.Document.BusinessDate)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
        if (issueLines.Count == 0)
            throw new BusinessRuleException("Hisoblash uchun mijoz saqlovida mahsulot yo'q.", "custody_empty");

        Dictionary<long, decimal> quantities;
        if (request.Lines is { Count: > 0 })
        {
            if (request.Lines.Select(x => x.GoodsIssueLineId).Distinct().Count() != request.Lines.Count)
                throw new BusinessRuleException("Hisob-kitob qatori takrorlangan.", "duplicate_settlement_line");
            var byId = issueLines.ToDictionary(x => x.Id);
            foreach (var input in request.Lines)
            {
                if (!byId.TryGetValue(input.GoodsIssueLineId, out var line))
                    throw new NotFoundException("Saqlov qatori topilmadi.", "goods_issue_line_not_found");
                if (input.Quantity > line.Quantity - line.ReturnedQuantity - line.SettledQuantity)
                    throw new BusinessRuleException("Hisoblanayotgan miqdor saqlov qoldig'idan oshdi.", "custody_quantity_exceeded");
            }
            quantities = request.Lines.ToDictionary(x => x.GoodsIssueLineId, x => x.Quantity);
            issueLines = issueLines.Where(x => quantities.ContainsKey(x.Id)).ToList();
        }
        else
        {
            quantities = issueLines.ToDictionary(x => x.Id,
                x => x.Quantity - x.ReturnedQuantity - x.SettledQuantity);
        }

        var saleItems = issueLines.Select(x => new CreateSaleItemDto(
            x.VariantId,
            quantities[x.Id],
            tradeCase.PricePolicy == TradeCasePricePolicy.SnapshotAtIssue ? x.UnitPrice : null,
            StockId: x.StockId,
            SourceCurrency: tradeCase.PricePolicy == TradeCasePricePolicy.SnapshotAtIssue ? x.PriceCurrency : null,
            SourceRate: tradeCase.PricePolicy == TradeCasePricePolicy.SnapshotAtIssue ? x.PriceRate : null)).ToList();
        var participants = await db.TradeCaseParticipants
            .Where(x => x.TradeCaseId == tradeCase.Id)
            .Select(x => new ParticipantInput(x.RoleDefinitionId, x.PartyId))
            .ToListAsync(cancellationToken);

        var saleResult = await sender.Send(new CreateSaleCommand(
            tradeCase.WarehouseId,
            tradeCase.CustomerId,
            request.PaidCash,
            request.PaidCard,
            request.PaidBonus,
            saleItems,
            request.DiscountAmount,
            request.Payments,
            request.DebtCurrency,
            request.DebtDueDate,
            StableSaleKey(tradeCase.Id, idempotencyKey),
            request.ApplyAutoDiscount,
            CreditAmount: 0,
            FromQueuedCart: false,
            UseCustomerAdvance: request.UseCustomerAdvance,
            Participants: participants,
            TradeCaseId: tradeCase.Id,
            StockAlreadyIssued: true), cancellationToken);

        var sale = await db.Sales.FirstAsync(x => x.Id == saleResult.SaleId, cancellationToken);
        var businessDate = request.BusinessDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var settlement = new TradeCaseSettlement
        {
            BranchId = tradeCase.BranchId,
            TradeCaseId = tradeCase.Id,
            SaleId = sale.Id,
            UserId = userId,
            DocumentNumber = await DocumentNumbers.NextAsync(db, "SET", businessDate, cancellationToken),
            BusinessDate = businessDate,
            Amount = sale.TotalAmount,
            Currency = await db.Businesses.Select(x => x.Currency).FirstAsync(cancellationToken),
            Note = NormalizeOptional(request.Note),
            IdempotencyKey = idempotencyKey
        };
        db.TradeCaseSettlements.Add(settlement);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var line in issueLines)
        {
            var quantity = quantities[line.Id];
            line.SettledQuantity += quantity;
            db.InventoryMovements.Add(new InventoryMovement
            {
                BranchId = tradeCase.BranchId,
                VariantId = line.VariantId,
                Quantity = quantity,
                Kind = InventoryMovementKind.Settlement,
                FromLocationKind = InventoryLocationKind.CustomerCustody,
                FromLocationId = tradeCase.Id,
                ToLocationKind = InventoryLocationKind.Customer,
                ToLocationId = tradeCase.CustomerId,
                SourceType = "TradeCaseSettlement",
                SourceId = settlement.Id,
                UserId = userId
            });
        }

        foreach (var group in issueLines.GroupBy(x => x.VariantId))
        {
            var delta = -group.Sum(x => quantities[x.Id]);
            if (!await db.AdjustInventoryPositionAsync(tradeCase.BranchId,
                    InventoryLocationKind.CustomerCustody, tradeCase.Id, group.Key,
                    delta, userId, cancellationToken))
                throw new ConflictException("Saqlov qoldig'i boshqa qurilmada o'zgargan. Qayta yuklang.", "custody_balance_conflict");
        }

        await db.SaveChangesAsync(cancellationToken);
        var hasRemaining = await db.GoodsIssueLines.AnyAsync(x =>
            x.Document.TradeCaseId == tradeCase.Id
            && x.Quantity > x.ReturnedQuantity + x.SettledQuantity, cancellationToken);
        if (!hasRemaining && request.CloseWhenEmpty)
        {
            tradeCase.Status = TradeCaseStatus.Settled;
            tradeCase.SettledAt = DateTime.UtcNow;
        }
        else if (!hasRemaining)
        {
            tradeCase.Status = TradeCaseStatus.SettlementPending;
        }
        else
        {
            tradeCase.Status = TradeCaseStatus.Open;
        }
        tradeCase.Version++;
        await db.SaveChangesAsync(cancellationToken);

        audit.SetOutcome("case.settled", "trade_case_settlements", settlement.Id, new
        {
            settlement.DocumentNumber,
            settlement.TradeCaseId,
            settlement.SaleId,
            settlement.Amount,
            caseStatus = tradeCase.Status,
            lines = issueLines.Select(x => new { x.Id, x.VariantId, Quantity = quantities[x.Id], x.StockId })
        }, hasRemaining ? "Loyiha qisman hisoblandi" : "Loyiha yakuniy hisoblandi", tradeCase.BranchId);

        return new TradeCaseSettlementCreatedDto(settlement.Id, settlement.DocumentNumber,
            sale.Id, sale.ReceiptToken, sale.TotalAmount,
            tradeCase.Status == TradeCaseStatus.Settled, tradeCase.Version);
    }

    private static string? StableSaleKey(long tradeCaseId, string? idempotencyKey)
    {
        if (idempotencyKey is null) return null;
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"case:{tradeCaseId}:{idempotencyKey}"));
        return $"case-{Convert.ToHexString(bytes).ToLowerInvariant()[..48]}";
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class SettleTradeCaseCommandValidator : AbstractValidator<SettleTradeCaseCommand>
{
    public SettleTradeCaseCommandValidator()
    {
        RuleFor(x => x.TradeCaseId).GreaterThan(0);
        RuleFor(x => x.PaidCash).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PaidCard).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PaidBonus).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DiscountAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Lines).Must(x => x is null || x.Count <= 500);
        RuleForEach(x => x.Lines!).ChildRules(x =>
        {
            x.RuleFor(y => y.GoodsIssueLineId).GreaterThan(0);
            x.RuleFor(y => y.Quantity).GreaterThan(0);
        }).When(x => x.Lines is not null);
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.IdempotencyKey).MaximumLength(128);
    }
}
