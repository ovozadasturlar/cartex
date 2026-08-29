using Cartex.Application.Common.Documents;
using Cartex.Application.Common.Finance;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Measurement;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Persistence.Services;
using Cartex.Shared.Models.Stocks;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.StockWriteOffs.Commands;

public sealed record StockWriteOffLineInput(
    long VariantId,
    decimal Quantity,
    StockWriteOffReason Reason,
    InventoryDisposition Disposition = InventoryDisposition.Scrap,
    long? StockId = null,
    string? Note = null);

public sealed record CreateStockWriteOffCommand(
    long WarehouseId,
    List<StockWriteOffLineInput> Lines,
    DateOnly? BusinessDate = null,
    string? Note = null,
    string? IdempotencyKey = null) : ICommand<StockWriteOffCreatedDto>;

public sealed class CreateStockWriteOffCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ILedgerService ledger,
    ISettingsService settings,
    IQuantityPolicyService quantityPolicy,
    IAuditService audit,
    InventoryReasonState inventoryReason) : IRequestHandler<CreateStockWriteOffCommand, StockWriteOffCreatedDto>
{
    private sealed record Allocation(StockWriteOffLineInput Input, Stock Batch, decimal Quantity);

    public Task<StockWriteOffCreatedDto> Handle(CreateStockWriteOffCommand request, CancellationToken cancellationToken) =>
        db.ExecuteInTransactionAsync(() => HandleCoreAsync(request, cancellationToken), cancellationToken);

    private async Task<StockWriteOffCreatedDto> HandleCoreAsync(CreateStockWriteOffCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");
        await WriteOffAccess.EnsureAllowedAsync(currentUser, settings, cancellationToken);

        var warehouse = await db.Warehouses.AsNoTracking()
            .Where(x => x.Id == request.WarehouseId)
            .Select(x => new { x.Id, x.BranchId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Warehouse not found.", "warehouse_not_found");
        if (!currentUser.CanAccessAllBranches && !currentUser.BranchIds.Contains(warehouse.BranchId))
            throw new NotFoundException("Warehouse not found.", "warehouse_not_found");

        var idempotencyKey = WriteOffDocuments.Normalize(request.IdempotencyKey);
        if (idempotencyKey is not null)
        {
            var existing = await db.StockWriteOffDocuments
                .Where(x => x.BranchId == warehouse.BranchId && x.IdempotencyKey == idempotencyKey)
                .Select(x => new StockWriteOffCreatedDto(x.Id, x.DocumentNumber, x.TotalCost,
                    x.Lines.Where(line => line.Disposition == InventoryDisposition.SupplierClaim).Sum(line => line.LineCost)))
                .FirstOrDefaultAsync(cancellationToken);
            if (existing is not null)
                return existing;
        }

        await quantityPolicy.ValidateAsync(request.Lines.Select(x => (x.VariantId, x.Quantity)), cancellationToken);

        var allocations = Allocate(request, await LockBatchesAsync(request, cancellationToken));
        var claims = await ResolveClaimsAsync(allocations, cancellationToken);

        var businessDate = request.BusinessDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var document = new StockWriteOffDocument
        {
            BranchId = warehouse.BranchId,
            WarehouseId = warehouse.Id,
            UserId = userId,
            DocumentNumber = await DocumentNumbers.NextAsync(db, "WOF", businessDate, cancellationToken),
            BusinessDate = businessDate,
            Note = WriteOffDocuments.Normalize(request.Note),
            IdempotencyKey = idempotencyKey
        };

        foreach (var allocation in allocations)
        {
            var claim = claims.GetValueOrDefault(allocation.Batch.Id);
            document.Lines.Add(new StockWriteOffLine
            {
                VariantId = allocation.Input.VariantId,
                Stock = allocation.Batch,
                SupplierId = allocation.Input.Disposition == InventoryDisposition.SupplierClaim ? claim?.SupplierId : null,
                Quantity = allocation.Quantity,
                UnitCost = allocation.Batch.PurchasePrice,
                LineCost = Math.Round(allocation.Quantity * allocation.Batch.PurchasePrice, 2),
                ClaimCurrency = allocation.Input.Disposition == InventoryDisposition.SupplierClaim ? claim?.Currency : null,
                ClaimRate = allocation.Input.Disposition == InventoryDisposition.SupplierClaim ? claim?.Rate ?? 1m : 1m,
                Reason = allocation.Input.Reason,
                Disposition = allocation.Input.Disposition,
                Note = WriteOffDocuments.Normalize(allocation.Input.Note)
            });
        }

        document.TotalCost = document.Lines.Sum(x => x.LineCost);

        db.StockWriteOffDocuments.Add(document);
        await db.SaveChangesAsync(cancellationToken);

        await WriteOffDocuments.MoveStockAsync(db, inventoryReason, document, cancellationToken);
        await WriteOffDocuments.PostSupplierClaimsAsync(ledger, document, userId, cancellationToken);
        var claimAmount = WriteOffDocuments.ClaimAmount(document.Lines);

        audit.SetOutcome("stock.written_off", "stock_write_off_documents", document.Id, new
        {
            document.DocumentNumber,
            document.WarehouseId,
            document.TotalCost,
            claimAmount,
            lines = document.Lines.Select(x => new
            {
                x.VariantId,
                x.StockId,
                x.Quantity,
                x.LineCost,
                x.Reason,
                x.Disposition,
                x.SupplierId
            })
        }, "Ombordan chiqim qilindi", document.BranchId);
        await db.SaveChangesAsync(cancellationToken);

        return new StockWriteOffCreatedDto(document.Id, document.DocumentNumber, document.TotalCost, claimAmount);
    }

    private async Task<List<Stock>> LockBatchesAsync(CreateStockWriteOffCommand request, CancellationToken cancellationToken)
    {
        var variantIds = request.Lines.Select(x => x.VariantId).Distinct().ToArray();
        var stockIds = request.Lines.Select(x => x.StockId).OfType<long>().Distinct().ToArray();
        return await db.LockAsync<Stock>(
            $"""
             SELECT * FROM stocks
             WHERE warehouse_id = {request.WarehouseId} AND is_deleted = false
               AND (variant_id = ANY({variantIds}) OR id = ANY({stockIds}))
             ORDER BY id FOR UPDATE
             """,
            cancellationToken);
    }

    private static List<Allocation> Allocate(CreateStockWriteOffCommand request, List<Stock> batches)
    {
        var byId = batches.ToDictionary(x => x.Id);
        var remaining = batches.ToDictionary(x => x.Id, x => x.Quantity);
        var allocations = new List<Allocation>();

        foreach (var line in request.Lines)
        {
            if (line.StockId is { } stockId)
            {
                var named = byId.GetValueOrDefault(stockId)
                    ?? throw new NotFoundException("Stock batch not found.", "stock_batch_not_found");
                if (named.VariantId != line.VariantId)
                    throw new BusinessRuleException("Partiya chiqim qatoriga mos emas.", "write_off_batch_mismatch");
                Take(line, named, line.Quantity);
                continue;
            }

            var rest = line.Quantity;
            foreach (var batch in batches
                         .Where(x => x.VariantId == line.VariantId && !x.IsDeficit)
                         .OrderBy(x => x.ExpiredAt == null).ThenBy(x => x.ExpiredAt).ThenBy(x => x.CreatedAt))
            {
                if (rest <= 0)
                    break;
                var take = Math.Min(remaining[batch.Id], rest);
                if (take <= 0)
                    continue;
                Take(line, batch, take);
                rest -= take;
            }
            if (rest > 0)
                throw new BusinessRuleException("Omborda yetarli mahsulot yo'q.", "insufficient_stock");
        }

        return allocations;

        void Take(StockWriteOffLineInput line, Stock batch, decimal quantity)
        {
            if (remaining[batch.Id] < quantity)
                throw new BusinessRuleException("Omborda yetarli mahsulot yo'q.", "insufficient_stock");
            remaining[batch.Id] -= quantity;
            allocations.Add(new Allocation(line, batch, quantity));
        }
    }

    private async Task<Dictionary<long, WriteOffClaim>> ResolveClaimsAsync(
        IReadOnlyCollection<Allocation> allocations,
        CancellationToken cancellationToken)
    {
        var claimed = allocations
            .Where(x => x.Input.Disposition == InventoryDisposition.SupplierClaim)
            .Select(x => x.Batch)
            .DistinctBy(x => x.Id)
            .ToList();
        if (claimed.Count == 0)
            return [];

        var supplyIds = claimed.Select(x => x.SupplyId).OfType<long>().Distinct().ToList();
        var supplies = await db.Supplies
            .Where(x => supplyIds.Contains(x.Id) && x.SupplierId != null && x.Supplier!.AcceptsReturns)
            .Select(x => new { x.Id, SupplierId = x.SupplierId!.Value, x.Currency, x.Rate })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var claims = new Dictionary<long, WriteOffClaim>();
        foreach (var batch in claimed)
        {
            if (batch.SupplyId is not { } supplyId || !supplies.TryGetValue(supplyId, out var supply))
                throw new BusinessRuleException(
                    "Bu partiya ta'minotchisi qaytarishni qabul qilmaydi.", "supplier_returns_not_accepted");
            claims[batch.Id] = new WriteOffClaim(supply.SupplierId, supply.Currency, supply.Rate);
        }
        return claims;
    }
}

internal sealed record WriteOffClaim(long SupplierId, string Currency, decimal Rate);

public sealed class CreateStockWriteOffCommandValidator : AbstractValidator<CreateStockWriteOffCommand>
{
    public CreateStockWriteOffCommandValidator()
    {
        RuleFor(x => x.WarehouseId).GreaterThan(0);
        RuleFor(x => x.Lines).NotEmpty().Must(x => x.Count <= 500);
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(x => x.VariantId).GreaterThan(0);
            line.RuleFor(x => x.Quantity).GreaterThan(0);
            line.RuleFor(x => x.Reason).IsInEnum();
            line.RuleFor(x => x.Disposition)
                .Must(x => x is InventoryDisposition.Scrap or InventoryDisposition.SupplierClaim)
                .WithMessage("Chiqim faqat brakka yoki ta'minotchiga da'voga yoziladi.");
            line.RuleFor(x => x.StockId).NotNull()
                .When(x => x.Disposition == InventoryDisposition.SupplierClaim)
                .WithMessage("Ta'minotchiga da'vo uchun partiya tanlanishi kerak.");
            line.RuleFor(x => x.StockId).GreaterThan(0).When(x => x.StockId.HasValue);
            line.RuleFor(x => x.Note).MaximumLength(500);
        });
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.IdempotencyKey).MaximumLength(128);
    }
}
