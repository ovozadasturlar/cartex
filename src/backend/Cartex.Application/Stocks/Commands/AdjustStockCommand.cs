using Cartex.Application.Common.Inventory;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;
using Cartex.Persistence.Services;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Application.Common.Measurement;

using Unit = Cartex.Application.Common.Messaging.Unit;

namespace Cartex.Application.Stocks.Commands;

public record AdjustStockCommand(long WarehouseId, long VariantId, decimal CountedQuantity, string? Reason) : ICommand<Unit>;

public sealed class AdjustStockCommandHandler(IApplicationDbContext db, ICurrentUser currentUser, IBranchCatalogService branchCatalog, IAuditService audit, IQuantityPolicyService quantityPolicy, IStockAllocator stockAllocator, InventoryReasonState inventoryReason)
    : IRequestHandler<AdjustStockCommand, Unit>
{
    public Task<Unit> Handle(AdjustStockCommand request, CancellationToken cancellationToken) =>
        db.ExecuteInTransactionAsync(() => HandleCoreAsync(request, cancellationToken), cancellationToken);

    private async Task<Unit> HandleCoreAsync(AdjustStockCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");

        var warehouse = await db.Warehouses.FirstOrDefaultAsync(w => w.Id == request.WarehouseId, cancellationToken)
            ?? throw new NotFoundException("Warehouse not found.");

        await quantityPolicy.ValidateAsync([(request.VariantId, request.CountedQuantity)], cancellationToken, allowZero: true);

        var stocks = await db.LockAsync<Stock>(
            $"SELECT * FROM stocks WHERE warehouse_id = {request.WarehouseId} AND variant_id = {request.VariantId} AND is_deleted = false ORDER BY id FOR UPDATE",
            cancellationToken);

        var systemQuantity = stocks.Sum(s => s.Quantity);
        var difference = request.CountedQuantity - systemQuantity;
        var stockId = stocks.MaxBy(s => s.Quantity)?.Id;

        var adjustment = new StockAdjustment
        {
            BranchId = warehouse.BranchId,
            WarehouseId = request.WarehouseId,
            VariantId = request.VariantId,
            StockId = stockId,
            SystemQuantity = systemQuantity,
            CountedQuantity = request.CountedQuantity,
            Difference = difference,
            Reason = request.Reason,
            UserId = userId
        };
        db.StockAdjustments.Add(adjustment);

        audit.Add("adjust", "stocks", stockId, new { request.WarehouseId, request.VariantId, systemQuantity, request.CountedQuantity, difference, request.Reason });

        await branchCatalog.ActivateAsync(warehouse.BranchId, [request.VariantId], BranchCatalogActivationSource.Adjustment, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        if (difference != 0)
        {
            inventoryReason.Declare(new(InventoryMovementKind.Adjustment, "StockAdjustment", adjustment.Id,
                InventoryLocation.External()));

            if (difference > 0)
            {
                var target = await stockAllocator.ResolveRestockBatchAsync(request.WarehouseId, request.VariantId, cancellationToken);
                target.Quantity += difference;
            }
            else
            {
                await stockAllocator.AllocateAsync(request.WarehouseId, request.VariantId, -difference, false, cancellationToken);
                stockAllocator.Apply();
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        return Unit.Value;
    }
}

public sealed class AdjustStockCommandValidator : AbstractValidator<AdjustStockCommand>
{
    public AdjustStockCommandValidator()
    {
        RuleFor(x => x.WarehouseId).GreaterThan(0);
        RuleFor(x => x.VariantId).GreaterThan(0);
        RuleFor(x => x.CountedQuantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}
