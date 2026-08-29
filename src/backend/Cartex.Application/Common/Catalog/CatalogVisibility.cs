using Cartex.Application.Common.Settings;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Common.Catalog;

public static class CatalogVisibility
{
    /// Savdoga chiqarilgan variantlar kesimi. Natija — kompozitsiyalanadigan `IQueryable`,
    /// shuning uchun sanoq va delta ham qatorlarni xotiraga tortmasdan shu yagona qoidadan
    /// foydalanadi: kesim ikki joyda ikki xil hisoblansa, kesh noto'g'ri narx bilan qolardi.
    public static async Task<IQueryable<ProductVariant>> ForSaleAsync(
        IQueryable<ProductVariant> source,
        IApplicationDbContext db,
        SalesPolicySettings policy,
        long branchId,
        long warehouseId,
        CancellationToken cancellationToken)
    {
        var catalog = await db.BranchCatalogEntries
            .Where(x => x.BranchId == branchId)
            .Select(x => new { x.VariantId, x.FirstActivityAt, x.VisibilityOverride })
            .ToListAsync(cancellationToken);
        var forceVisible = catalog
            .Where(x => x.VisibilityOverride == BranchCatalogVisibilityOverride.ForceVisible)
            .Select(x => x.VariantId)
            .ToArray();
        var forceHidden = catalog
            .Where(x => x.VisibilityOverride == BranchCatalogVisibilityOverride.ForceHidden)
            .Select(x => x.VariantId)
            .ToArray();

        var query = source.Where(v => v.Product.IsEnabled);
        if (forceHidden.Length > 0)
            query = query.Where(v => !forceHidden.Contains(v.Id));

        if (!policy.ShowUnlistedProducts)
        {
            var listed = catalog.Where(x => x.FirstActivityAt != null).Select(x => x.VariantId)
                .Concat(forceVisible).Distinct().ToArray();
            query = listed.Length == 0
                ? query.Where(_ => false)
                : query.Where(v => listed.Contains(v.Id));
        }

        if (!policy.ShowOutOfStock)
        {
            var inStock = await db.Stocks
                .Where(s => s.WarehouseId == warehouseId)
                .GroupBy(s => s.VariantId)
                .Where(g => g.Sum(s => s.Quantity) > 0)
                .Select(g => g.Key)
                .ToArrayAsync(cancellationToken);
            var visible = inStock.Concat(forceVisible).Distinct().ToArray();
            query = visible.Length == 0
                ? query.Where(_ => false)
                : query.Where(v => visible.Contains(v.Id));
        }

        return query;
    }
}
