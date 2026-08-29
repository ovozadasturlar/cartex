using Cartex.Application.Common.Inventory;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence.Services;

using Unit = Cartex.Application.Common.Messaging.Unit;

namespace Cartex.Application.StockTransfers.Commands;

public record ReceiveStockTransferCommand(long Id) : ICommand<Unit>;

public sealed class ReceiveStockTransferCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IBranchCatalogService branchCatalog,
    InventoryReasonState inventoryReason) : IRequestHandler<ReceiveStockTransferCommand, Unit>
{
    public async Task<Unit> Handle(ReceiveStockTransferCommand request, CancellationToken cancellationToken)
    {
        var transfer = (await db.LockAsync<StockTransfer>(
                $"SELECT * FROM stock_transfers WHERE id = {request.Id} AND is_deleted = false FOR UPDATE",
                cancellationToken)).FirstOrDefault()
            ?? throw new NotFoundException("Transfer not found.");

        if (transfer.Status != TransferStatus.Sent)
            throw new BusinessRuleException("Transfer is not in Sent status.");

        var toWarehouse = await db.Warehouses.FirstOrDefaultAsync(w => w.Id == transfer.ToWarehouseId, cancellationToken)
            ?? throw new NotFoundException("Target warehouse not found.");

        if (toWarehouse.AssignedUserId != currentUser.UserId
            && !currentUser.HasPermission(AppPermissions.StockTransfers.ReceiveAny))
            throw new ForbiddenException("Bu yuk xatini qabul qilishga ruxsat yo'q.");

        transfer.Status = TransferStatus.Received;

        inventoryReason.Declare(new(InventoryMovementKind.Transfer, "StockTransfer", transfer.Id,
            InventoryLocation.Warehouse(transfer.FromWarehouseId),
            InventoryLocation.Warehouse(transfer.ToWarehouseId)));

        long[] warehouseIds = [transfer.FromWarehouseId, transfer.ToWarehouseId];
        var batches = await db.LockAsync<Stock>(
            $"SELECT * FROM stocks WHERE warehouse_id = ANY({warehouseIds}) AND variant_id = {transfer.VariantId} AND is_deleted = false ORDER BY id FOR UPDATE",
            cancellationToken);

        var sourceStocks = batches.Where(s => s.WarehouseId == transfer.FromWarehouseId && s.Quantity > 0).ToList();
        var targetStocks = batches.Where(s => s.WarehouseId == transfer.ToWarehouseId).ToList();

        var available = sourceStocks.Sum(s => s.Quantity);
        if (available < transfer.Quantity)
            throw new BusinessRuleException($"Manba omborda qoldiq yetarli emas: kerak {transfer.Quantity:0.###}, mavjud {available:0.###}.");

        var remaining = transfer.Quantity;

        foreach (var stock in sourceStocks)
        {
            if (remaining <= 0) break;

            var deduct = Math.Min(stock.Quantity, remaining);
            stock.Quantity -= deduct;
            remaining -= deduct;

            var targetStock = targetStocks.FirstOrDefault(s =>
                s.PurchasePrice == stock.PurchasePrice && s.ExpiredAt == stock.ExpiredAt);

            if (targetStock is not null)
            {
                targetStock.Quantity += deduct;
            }
            else
            {
                targetStock = new Stock
                {
                    BranchId = toWarehouse.BranchId,
                    VariantId = transfer.VariantId,
                    WarehouseId = transfer.ToWarehouseId,
                    Quantity = deduct,
                    PurchasePrice = stock.PurchasePrice,
                    ExpiredAt = stock.ExpiredAt
                };
                db.Stocks.Add(targetStock);
                targetStocks.Add(targetStock);
            }
        }

        await branchCatalog.ActivateAsync(toWarehouse.BranchId, [transfer.VariantId], BranchCatalogActivationSource.Transfer, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
