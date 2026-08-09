using Cartex.Application.Common.Documents;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Measurement;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Shared.Models.TradeCases;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.TradeCases.Commands;

public sealed record GoodsReturnLineInput(
    long GoodsIssueLineId,
    decimal Quantity,
    string? Reason,
    ReturnItemCondition Condition,
    InventoryDisposition Disposition);

public sealed record CreateGoodsReturnCommand(
    long TradeCaseId,
    List<GoodsReturnLineInput> Lines,
    DateOnly? BusinessDate = null,
    string? Note = null,
    string? IdempotencyKey = null,
    int? ExpectedCaseVersion = null) : ICommand<GoodsReturnCreatedDto>;

public sealed class CreateGoodsReturnCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IQuantityPolicyService quantityPolicy,
    IAuditService audit) : IRequestHandler<CreateGoodsReturnCommand, GoodsReturnCreatedDto>
{
    public async Task<GoodsReturnCreatedDto> Handle(CreateGoodsReturnCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");
        if (!currentUser.HasPermission(AppPermissions.GoodsIssues.Return))
            throw new ForbiddenException("Saqlovdagi mahsulotni qaytarib olishga ruxsat yo'q.");

        var tradeCase = await db.TradeCases
            .FromSqlInterpolated($"SELECT * FROM trade_cases WHERE id = {request.TradeCaseId} FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Trade case not found.", "trade_case_not_found");

        var idempotencyKey = NormalizeOptional(request.IdempotencyKey);
        if (idempotencyKey is not null)
        {
            var existing = await db.GoodsReturnDocuments
                .Where(x => x.BranchId == tradeCase.BranchId && x.IdempotencyKey == idempotencyKey)
                .Select(x => new GoodsReturnCreatedDto(x.Id, x.DocumentNumber, x.TradeCase.Version))
                .FirstOrDefaultAsync(cancellationToken);
            if (existing is not null) return existing;
        }

        if (tradeCase.Status is TradeCaseStatus.Settled or TradeCaseStatus.Cancelled)
            throw new BusinessRuleException("Yopilgan loyihaga qaytaruv kiritib bo'lmaydi.", "trade_case_closed");
        if (request.ExpectedCaseVersion.HasValue && request.ExpectedCaseVersion != tradeCase.Version)
            throw new ConflictException("Loyiha boshqa qurilmada yangilangan. Ma'lumotni qayta yuklang.", "trade_case_version_conflict");
        if (request.Lines.Select(x => x.GoodsIssueLineId).Distinct().Count() != request.Lines.Count)
            throw new BusinessRuleException("Bitta berilgan qator takrorlanmasligi kerak.", "duplicate_goods_return_line");
        if (request.Lines.Any(x => x.Disposition == InventoryDisposition.SellableRestock
                                   && x.Condition != ReturnItemCondition.Sellable))
            throw new BusinessRuleException("Faqat sotishga yaroqli mahsulot ombor qoldig'iga qaytariladi.", "invalid_return_disposition");

        var ids = request.Lines.Select(x => x.GoodsIssueLineId).ToList();
        var issueLines = await db.GoodsIssueLines
            .Where(x => ids.Contains(x.Id) && x.Document.TradeCaseId == tradeCase.Id)
            .Include(x => x.Stock)
            .ToListAsync(cancellationToken);
        if (issueLines.Count != ids.Count)
            throw new NotFoundException("Berilgan mahsulot qatori topilmadi.", "goods_issue_line_not_found");
        var byId = issueLines.ToDictionary(x => x.Id);
        foreach (var input in request.Lines)
        {
            var line = byId[input.GoodsIssueLineId];
            if (input.Quantity > line.Quantity - line.ReturnedQuantity - line.SettledQuantity)
                throw new BusinessRuleException("Qaytariladigan miqdor mijozdagi qoldiqdan oshdi.", "custody_quantity_exceeded");
        }

        await quantityPolicy.ValidateAsync(request.Lines.Select(x =>
            (byId[x.GoodsIssueLineId].VariantId, x.Quantity)), cancellationToken);

        var businessDate = request.BusinessDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var document = new GoodsReturnDocument
        {
            BranchId = tradeCase.BranchId,
            TradeCaseId = tradeCase.Id,
            WarehouseId = tradeCase.WarehouseId,
            CustomerId = tradeCase.CustomerId,
            UserId = userId,
            DocumentNumber = await DocumentNumbers.NextAsync(db, "GRT", businessDate, cancellationToken),
            BusinessDate = businessDate,
            Note = NormalizeOptional(request.Note),
            IdempotencyKey = idempotencyKey
        };
        foreach (var input in request.Lines)
        {
            var issueLine = byId[input.GoodsIssueLineId];
            document.Lines.Add(new GoodsReturnLine
            {
                GoodsIssueLineId = issueLine.Id,
                VariantId = issueLine.VariantId,
                Quantity = input.Quantity,
                Reason = NormalizeOptional(input.Reason),
                Condition = input.Condition,
                Disposition = input.Disposition
            });
        }
        db.GoodsReturnDocuments.Add(document);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var line in document.Lines)
        {
            var issueLine = byId[line.GoodsIssueLineId];
            issueLine.ReturnedQuantity += line.Quantity;
            if (line.Disposition == InventoryDisposition.SellableRestock)
                issueLine.Stock.Quantity += line.Quantity;
            else
                await db.UpsertInventoryPositionAsync(tradeCase.BranchId, LocationFor(line.Disposition),
                    tradeCase.WarehouseId, line.VariantId, line.Quantity, userId, cancellationToken);

            db.InventoryMovements.Add(new InventoryMovement
            {
                BranchId = tradeCase.BranchId,
                VariantId = line.VariantId,
                Quantity = line.Quantity,
                Kind = InventoryMovementKind.GoodsReturn,
                FromLocationKind = InventoryLocationKind.CustomerCustody,
                FromLocationId = tradeCase.Id,
                ToLocationKind = line.Disposition == InventoryDisposition.SellableRestock
                    ? InventoryLocationKind.Warehouse
                    : LocationFor(line.Disposition),
                ToLocationId = tradeCase.WarehouseId,
                SourceType = "GoodsReturn",
                SourceId = document.Id,
                UserId = userId
            });
        }

        foreach (var group in document.Lines.GroupBy(x => x.VariantId))
        {
            var adjusted = await db.AdjustInventoryPositionAsync(tradeCase.BranchId,
                InventoryLocationKind.CustomerCustody, tradeCase.Id, group.Key,
                -group.Sum(x => x.Quantity), userId, cancellationToken);
            if (!adjusted)
                throw new ConflictException("Saqlov qoldig'i boshqa qurilmada o'zgargan. Qayta yuklang.", "custody_balance_conflict");
        }

        tradeCase.Version++;
        await db.SaveChangesAsync(cancellationToken);
        audit.SetOutcome("goods.returned_from_custody", "goods_return_documents", document.Id, new
        {
            document.DocumentNumber,
            document.TradeCaseId,
            lines = document.Lines.Select(x => new
            {
                x.GoodsIssueLineId,
                x.VariantId,
                x.Quantity,
                x.Condition,
                x.Disposition,
                x.Reason
            })
        }, "Saqlovdagi mahsulot qaytarib olindi", tradeCase.BranchId);
        return new GoodsReturnCreatedDto(document.Id, document.DocumentNumber, tradeCase.Version);
    }

    private static InventoryLocationKind LocationFor(InventoryDisposition disposition) => disposition switch
    {
        InventoryDisposition.Quarantine => InventoryLocationKind.Quarantine,
        InventoryDisposition.Scrap => InventoryLocationKind.Scrap,
        InventoryDisposition.SupplierClaim => InventoryLocationKind.SupplierClaim,
        _ => InventoryLocationKind.Warehouse
    };

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class CreateGoodsReturnCommandValidator : AbstractValidator<CreateGoodsReturnCommand>
{
    public CreateGoodsReturnCommandValidator()
    {
        RuleFor(x => x.TradeCaseId).GreaterThan(0);
        RuleFor(x => x.Lines).NotEmpty().Must(x => x.Count <= 500);
        RuleForEach(x => x.Lines).ChildRules(x =>
        {
            x.RuleFor(y => y.GoodsIssueLineId).GreaterThan(0);
            x.RuleFor(y => y.Quantity).GreaterThan(0);
            x.RuleFor(y => y.Reason).MaximumLength(500);
        });
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.IdempotencyKey).MaximumLength(128);
    }
}
