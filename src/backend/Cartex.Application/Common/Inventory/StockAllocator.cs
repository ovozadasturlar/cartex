using Cartex.Domain.Entities;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Common.Inventory;

public record StockAllocation(Stock Batch, decimal Quantity);

public interface IStockAllocator
{
    Task PreloadAsync(long warehouseId, IEnumerable<long> variantIds, CancellationToken cancellationToken);
    Task<IReadOnlyList<StockAllocation>> AllocateAsync(long warehouseId, long variantId, decimal quantity, bool allowInsufficientStock, CancellationToken cancellationToken);
    Task<Stock> ResolveRestockBatchAsync(long warehouseId, long variantId, CancellationToken cancellationToken);
}

public sealed class StockAllocator(IApplicationDbContext db) : IStockAllocator
{
    private readonly Dictionary<(long WarehouseId, long VariantId), List<Stock>> _cache = [];

    public async Task PreloadAsync(long warehouseId, IEnumerable<long> variantIds, CancellationToken cancellationToken)
    {
        var ids = variantIds.Distinct().Where(id => !_cache.ContainsKey((warehouseId, id))).ToList();
        if (ids.Count == 0) return;

        var idArray = ids.ToArray();
        var batches = await db.Stocks
            .FromSqlInterpolated($"SELECT * FROM stocks WHERE warehouse_id = {warehouseId} AND variant_id = ANY({idArray}) AND quantity > 0 AND is_deleted = false FOR UPDATE")
            .ToListAsync(cancellationToken);

        foreach (var id in ids)
            _cache[(warehouseId, id)] = SortBatches(batches.Where(b => b.VariantId == id));
    }

    private static List<Stock> SortBatches(IEnumerable<Stock> batches) =>
        [.. batches.OrderBy(s => s.ExpiredAt == null).ThenBy(s => s.ExpiredAt).ThenBy(s => s.CreatedAt)];

    public async Task<IReadOnlyList<StockAllocation>> AllocateAsync(long warehouseId, long variantId, decimal quantity, bool allowInsufficientStock, CancellationToken cancellationToken)
    {
        var key = (warehouseId, variantId);
        if (!_cache.TryGetValue(key, out var batches))
        {
            batches = SortBatches(await db.Stocks
                .FromSqlInterpolated($"SELECT * FROM stocks WHERE warehouse_id = {warehouseId} AND variant_id = {variantId} AND quantity > 0 AND is_deleted = false FOR UPDATE")
                .ToListAsync(cancellationToken));
            _cache[key] = batches;
        }

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
        {
            if (!allowInsufficientStock)
                throw new BusinessRuleException("Omborda yetarli mahsulot yo'q.");
            var deficit = await GetDeficitAsync(warehouseId, variantId, cancellationToken);
            if (!batches.Contains(deficit))
                batches.Add(deficit);
            allocations.Add(new StockAllocation(deficit, remaining));
            remaining = 0;
        }

        return allocations;
    }
    public async Task<Stock> ResolveRestockBatchAsync(long warehouseId, long variantId, CancellationToken cancellationToken)
    {
        var batch = await db.Stocks
            .FromSqlInterpolated($"SELECT * FROM stocks WHERE warehouse_id = {warehouseId} AND variant_id = {variantId} AND is_deficit = false AND is_deleted = false ORDER BY id DESC LIMIT 1 FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);
        if (batch is not null)
            return batch;

        var branchId = await db.Warehouses
            .Where(x => x.Id == warehouseId)
            .Select(x => x.BranchId)
            .FirstOrDefaultAsync(cancellationToken);
        if (branchId == 0)
            throw new NotFoundException("Warehouse not found.");

        var purchasePrice = await db.Stocks
            .Where(x => x.VariantId == variantId)
            .OrderByDescending(x => x.Id)
            .Select(x => (decimal?)x.PurchasePrice)
            .FirstOrDefaultAsync(cancellationToken) ?? 0;

        batch = new Stock
        {
            BranchId = branchId,
            WarehouseId = warehouseId,
            VariantId = variantId,
            PurchasePrice = purchasePrice
        };
        db.Stocks.Add(batch);
        return batch;
    }

    private async Task<Stock> GetDeficitAsync(long warehouseId, long variantId, CancellationToken cancellationToken)
    {
        var deficit = await db.Stocks
            .FromSqlInterpolated($"SELECT * FROM stocks WHERE warehouse_id = {warehouseId} AND variant_id = {variantId} AND is_deficit = true AND is_deleted = false FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);
        if (deficit is not null)
            return deficit;

        var variant = await db.ProductVariants
            .FromSqlInterpolated($"SELECT * FROM product_variants WHERE id = {variantId} AND is_deleted = false FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (variant is null)
            throw new NotFoundException("Product variant not found.");

        deficit = await db.Stocks
            .FromSqlInterpolated($"SELECT * FROM stocks WHERE warehouse_id = {warehouseId} AND variant_id = {variantId} AND is_deficit = true AND is_deleted = false FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);
        if (deficit is not null)
            return deficit;

        var branchId = await db.Warehouses
            .Where(x => x.Id == warehouseId)
            .Select(x => x.BranchId)
            .FirstOrDefaultAsync(cancellationToken);
        if (branchId == 0)
            throw new NotFoundException("Warehouse not found.");

        deficit = new Stock
        {
            BranchId = branchId,
            WarehouseId = warehouseId,
            VariantId = variantId,
            IsDeficit = true
        };
        db.Stocks.Add(deficit);
        return deficit;
    }
}
