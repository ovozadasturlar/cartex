using Cartex.Domain.Entities;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Common.Inventory;

public record StockAllocation(Stock Batch, decimal Quantity);

public interface IStockAllocator
{
    Task PreloadAsync(long warehouseId, IEnumerable<long> variantIds, CancellationToken cancellationToken);
    Task<IReadOnlyList<StockAllocation>> AllocateAsync(long warehouseId, long variantId, decimal quantity, CancellationToken cancellationToken);
}

public sealed class StockAllocator(IApplicationDbContext db) : IStockAllocator
{
    private readonly Dictionary<(long WarehouseId, long VariantId), List<Stock>> _cache = [];

    public async Task PreloadAsync(long warehouseId, IEnumerable<long> variantIds, CancellationToken cancellationToken)
    {
        var ids = variantIds.Distinct().Where(id => !_cache.ContainsKey((warehouseId, id))).ToList();
        if (ids.Count == 0) return;

        var batches = await db.Stocks
            .Where(s => s.WarehouseId == warehouseId && ids.Contains(s.VariantId) && s.Quantity > 0)
            .ToListAsync(cancellationToken);

        foreach (var id in ids)
            _cache[(warehouseId, id)] = SortBatches(batches.Where(b => b.VariantId == id));
    }

    private static List<Stock> SortBatches(IEnumerable<Stock> batches) =>
        [.. batches.OrderBy(s => s.ExpiredAt == null).ThenBy(s => s.ExpiredAt).ThenBy(s => s.CreatedAt)];

    public async Task<IReadOnlyList<StockAllocation>> AllocateAsync(long warehouseId, long variantId, decimal quantity, CancellationToken cancellationToken)
    {
        if (!_cache.TryGetValue((warehouseId, variantId), out var batches))
            batches = SortBatches(await db.Stocks
                .Where(s => s.WarehouseId == warehouseId && s.VariantId == variantId && s.Quantity > 0)
                .ToListAsync(cancellationToken));

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
