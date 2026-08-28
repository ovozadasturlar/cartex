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
    void Apply();
}

public sealed class StockAllocator(IApplicationDbContext db) : IStockAllocator
{
    private readonly TransactionScoped<Dictionary<(long WarehouseId, long VariantId), List<Stock>>> _batches = new(db, () => []);
    private readonly TransactionScoped<Dictionary<Stock, decimal>> _reservations = new(db, () => []);

    private Dictionary<(long WarehouseId, long VariantId), List<Stock>> Cache => _batches.Value;
    private Dictionary<Stock, decimal> Reserved => _reservations.Value;

    public async Task PreloadAsync(long warehouseId, IEnumerable<long> variantIds, CancellationToken cancellationToken)
    {
        var cache = Cache;
        var ids = variantIds.Distinct().Where(id => !cache.ContainsKey((warehouseId, id))).ToList();
        if (ids.Count == 0) return;

        var idArray = ids.ToArray();
        var batches = await LockAsync(
            $"SELECT * FROM stocks WHERE warehouse_id = {warehouseId} AND variant_id = ANY({idArray}) AND quantity > 0 AND is_deleted = false FOR UPDATE",
            cancellationToken);

        foreach (var id in ids)
            cache[(warehouseId, id)] = SortBatches(batches.Where(b => b.VariantId == id));
    }

    /// Qator qulfi olinganda uning qiymati bazadan qayta o'qilishi shart: EF allaqachon
    /// kuzatilayotgan obyektni qaytaradi va u oldingi tranzaksiyadagi eskirgan qiymatni
    /// saqlab qolgan bo'lishi mumkin (`LedgerService.LockAsync` bilan bir xil qoida).
    private async Task<List<Stock>> LockAsync(FormattableString sql, CancellationToken cancellationToken)
    {
        var tracked = db.Stocks.Local.Select(x => x.Id).ToHashSet();
        var batches = await db.Stocks.FromSqlInterpolated(sql).ToListAsync(cancellationToken);
        foreach (var batch in batches)
            if (tracked.Contains(batch.Id))
                await db.ReloadAsync(batch, cancellationToken);
        return batches;
    }

    private static List<Stock> SortBatches(IEnumerable<Stock> batches) =>
        [.. batches.OrderBy(s => s.ExpiredAt == null).ThenBy(s => s.ExpiredAt).ThenBy(s => s.CreatedAt)];

    public async Task<IReadOnlyList<StockAllocation>> AllocateAsync(long warehouseId, long variantId, decimal quantity, bool allowInsufficientStock, CancellationToken cancellationToken)
    {
        var cache = Cache;
        var reserved = Reserved;
        var key = (warehouseId, variantId);
        if (!cache.TryGetValue(key, out var batches))
        {
            batches = SortBatches(await LockAsync(
                $"SELECT * FROM stocks WHERE warehouse_id = {warehouseId} AND variant_id = {variantId} AND quantity > 0 AND is_deleted = false FOR UPDATE",
                cancellationToken));
            cache[key] = batches;
        }

        var allocations = new List<StockAllocation>();
        var remaining = quantity;

        foreach (var batch in batches)
        {
            if (remaining <= 0)
                break;

            var available = batch.Quantity - reserved.GetValueOrDefault(batch);
            if (available <= 0)
                continue;

            var take = Math.Min(available, remaining);
            allocations.Add(new StockAllocation(batch, take));
            reserved[batch] = reserved.GetValueOrDefault(batch) + take;
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
            reserved[deficit] = reserved.GetValueOrDefault(deficit) + remaining;
        }

        return allocations;
    }

    public void Apply()
    {
        var reserved = Reserved;
        foreach (var (batch, quantity) in reserved)
            batch.Quantity -= quantity;
        reserved.Clear();
    }

    public async Task<Stock> ResolveRestockBatchAsync(long warehouseId, long variantId, CancellationToken cancellationToken)
    {
        var batch = (await LockAsync(
            $"SELECT * FROM stocks WHERE warehouse_id = {warehouseId} AND variant_id = {variantId} AND is_deficit = false AND is_deleted = false ORDER BY id DESC LIMIT 1 FOR UPDATE",
            cancellationToken)).FirstOrDefault();
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
        var deficit = await FindDeficitAsync(warehouseId, variantId, cancellationToken);
        if (deficit is not null)
            return deficit;

        var variant = await db.ProductVariants
            .FromSqlInterpolated($"SELECT * FROM product_variants WHERE id = {variantId} AND is_deleted = false FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (variant is null)
            throw new NotFoundException("Product variant not found.");

        deficit = await FindDeficitAsync(warehouseId, variantId, cancellationToken);
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

    private async Task<Stock?> FindDeficitAsync(long warehouseId, long variantId, CancellationToken cancellationToken) =>
        (await LockAsync(
            $"SELECT * FROM stocks WHERE warehouse_id = {warehouseId} AND variant_id = {variantId} AND is_deficit = true AND is_deleted = false FOR UPDATE",
            cancellationToken)).FirstOrDefault();
}
