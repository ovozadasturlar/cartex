using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;

using Unit = Cartex.Application.Common.Messaging.Unit;

namespace Cartex.Application.StockTransfers.Commands;

public record ReceiveStockTransferCommand(long Id) : ICommand<Unit>;

public sealed class ReceiveStockTransferCommandHandler(IApplicationDbContext db, ICurrentUser currentUser) : IRequestHandler<ReceiveStockTransferCommand, Unit>
{
    public async Task<Unit> Handle(ReceiveStockTransferCommand request, CancellationToken cancellationToken)
    {
        var transfer = await db.StockTransfers
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Transfer not found.");

        if (transfer.Status != TransferStatus.Sent)
            throw new BusinessRuleException("Transfer is not in Sent status.");

        var toWarehouse = await db.Warehouses.FirstOrDefaultAsync(w => w.Id == transfer.ToWarehouseId, cancellationToken)
            ?? throw new NotFoundException("Target warehouse not found.");

        if (toWarehouse.AssignedUserId != currentUser.UserId && !currentUser.HasPermission(AppPermissions.StockTransfers.Manage))
            throw new ForbiddenException("Bu yuk xatini qabul qilishga ruxsat yo'q.");

        transfer.Status = TransferStatus.Received;

        var sourceStocks = await db.Stocks
            .Where(s => s.WarehouseId == transfer.FromWarehouseId && s.VariantId == transfer.VariantId && s.Quantity > 0)
            .OrderBy(s => s.Id)
            .ToListAsync(cancellationToken);

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

            var targetStock = await db.Stocks
                .FirstOrDefaultAsync(s =>
                    s.WarehouseId == transfer.ToWarehouseId &&
                    s.VariantId == transfer.VariantId &&
                    s.PurchasePrice == stock.PurchasePrice &&
                    s.ExpiredAt == stock.ExpiredAt, cancellationToken);

            if (targetStock is not null)
            {
                targetStock.Quantity += deduct;
            }
            else
            {
                db.Stocks.Add(new Stock
                {
                    BranchId = toWarehouse.BranchId,
                    VariantId = transfer.VariantId,
                    WarehouseId = transfer.ToWarehouseId,
                    Quantity = deduct,
                    PurchasePrice = stock.PurchasePrice,
                    ExpiredAt = stock.ExpiredAt
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
