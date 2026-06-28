using Cartex.Domain.Entities;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Common.Inventory;

public record StockAllocation(Stock Batch, decimal Quantity);

public interface IStockAllocator
{
    Task<IReadOnlyList<StockAllocation>> AllocateAsync(long warehouseId, long productId, decimal quantity, CancellationToken cancellationToken);
}

public sealed class StockAllocator(IApplicationDbContext db) : IStockAllocator
{
    public async Task<IReadOnlyList<StockAllocation>> AllocateAsync(long warehouseId, long productId, decimal quantity, CancellationToken cancellationToken)
    {
        var batches = await db.Stocks
            .Where(s => s.WarehouseId == warehouseId && s.ProductId == productId && s.Quantity > 0)
            .OrderBy(s => s.ExpiredAt == null)
            .ThenBy(s => s.ExpiredAt)
            .ThenBy(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        var allocations = new List<StockAllocation>();
        var remaining = quantity;

        foreach (var batch in batches)
        {
            if (remaining <= 0)
                break;

            var take = Math.Min(batch.Quantity, remaining);
            allocations.Add(new StockAllocation(batch, take));
            remaining -= take;
        }

        if (remaining > 0)
            throw new BusinessRuleException("Omborda yetarli mahsulot yo'q.");

        return allocations;
    }
}
