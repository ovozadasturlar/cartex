using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Shared.Models.Stocks;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Stocks.Commands;

public sealed record BackfillOpeningStockCommand : ICommand<OpeningStockBackfillDto>;

public sealed class BackfillOpeningStockCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IAuditService audit) : IRequestHandler<BackfillOpeningStockCommand, OpeningStockBackfillDto>
{
    private const string Source = "OpeningBalance";

    public async Task<OpeningStockBackfillDto> Handle(BackfillOpeningStockCommand request, CancellationToken cancellationToken)
    {
        var journalled = await db.InventoryMovements
            .Where(x => x.StockId != null)
            .GroupBy(x => x.StockId)
            .Select(x => new { StockId = x.Key, Quantity = x.Sum(m => m.Quantity) })
            .ToListAsync(cancellationToken);
        var balanced = journalled.ToDictionary(x => x.StockId!.Value, x => x.Quantity);

        var stocks = await db.Stocks
            .Where(x => x.Quantity != 0)
            .Select(x => new { x.Id, x.BranchId, x.WarehouseId, x.VariantId, x.Quantity })
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var written = 0;
        var total = 0m;

        foreach (var stock in stocks)
        {
            var delta = stock.Quantity - balanced.GetValueOrDefault(stock.Id);
            if (delta == 0)
                continue;

            var inbound = delta > 0;
            db.InventoryMovements.Add(new InventoryMovement
            {
                BranchId = stock.BranchId,
                WarehouseId = stock.WarehouseId,
                VariantId = stock.VariantId,
                StockId = stock.Id,
                Quantity = delta,
                Kind = InventoryMovementKind.Opening,
                FromLocationKind = inbound ? InventoryLocationKind.External : InventoryLocationKind.Warehouse,
                FromLocationId = inbound ? 0 : stock.WarehouseId,
                ToLocationKind = inbound ? InventoryLocationKind.Warehouse : InventoryLocationKind.External,
                ToLocationId = inbound ? stock.WarehouseId : 0,
                SourceType = Source,
                SourceId = stock.Id,
                UserId = currentUser.UserId,
                OccurredAt = now
            });
            written++;
            total += delta;
        }

        audit.Add("stock.openingBackfill", "inventory_movements", null,
            new { movements = written, quantity = total, scanned = stocks.Count });
        await db.SaveChangesAsync(cancellationToken);

        return new OpeningStockBackfillDto(written, total, stocks.Count - written);
    }
}
