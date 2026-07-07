using Cartex.Application.Common.Interfaces;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Stocks.Queries;

public record GetStockOnHandQuery(long WarehouseId, long? CategoryId = null, string? Search = null, int Page = 1, int PageSize = 50)
    : IRequest<StockOnHandPageDto>;

public record StockOnHandDto(long VariantId, string ProductName, long? CategoryId, string? CategoryName, string UnitName, string Dimension, decimal Quantity, decimal SellingPrice, DateOnly? NearestExpiry, string? ImageUrl = null);

public record StockOnHandPageDto(IReadOnlyCollection<StockOnHandDto> Items, int TotalCount, decimal TotalQuantity, decimal TotalValue);

public sealed class GetStockOnHandQueryHandler(IApplicationDbContext db, IObjectStorage storage) : IRequestHandler<GetStockOnHandQuery, StockOnHandPageDto>
{
    public async Task<StockOnHandPageDto> Handle(GetStockOnHandQuery request, CancellationToken cancellationToken)
    {
        var onHand = await db.Stocks
            .Where(s => s.WarehouseId == request.WarehouseId)
            .GroupBy(s => s.VariantId)
            .Select(g => new { VariantId = g.Key, OnHand = g.Sum(s => s.Quantity), NearestExpiry = g.Min(s => s.ExpiredAt) })
            .ToListAsync(cancellationToken);

        if (onHand.Count == 0)
            return new StockOnHandPageDto([], 0, 0, 0);

        var variantIds = onHand.Select(o => o.VariantId).ToList();

        var variants = await db.ProductVariants
            .Where(v => variantIds.Contains(v.Id))
            .Select(v => new { v.Id, ProductName = v.Product.Name, v.Product.CategoryId, CategoryName = v.Product.Category != null ? v.Product.Category.Name : null, UnitName = v.Product.Unit.Name, Dimension = v.Product.Unit.Dimension, ImageKey = v.ImageKey ?? v.Product.ImageKey })
            .ToDictionaryAsync(v => v.Id, cancellationToken);

        var prices = await db.ProductPrices
            .Where(pp => variantIds.Contains(pp.VariantId) && (pp.WarehouseId == request.WarehouseId || pp.WarehouseId == null))
            .ToListAsync(cancellationToken);

        decimal PriceOf(long variantId) =>
            (prices.FirstOrDefault(p => p.VariantId == variantId && p.WarehouseId == request.WarehouseId)
             ?? prices.FirstOrDefault(p => p.VariantId == variantId && p.WarehouseId == null))?.SellingPrice ?? 0;

        HashSet<long>? subtree = null;
        if (request.CategoryId is { } categoryId)
        {
            var cats = await db.Categories.Select(c => new { c.Id, c.ParentId }).ToListAsync(cancellationToken);
            subtree = [categoryId];
            var added = true;
            while (added)
            {
                added = false;
                foreach (var c in cats)
                    if (c.ParentId is { } pid && subtree.Contains(pid) && subtree.Add(c.Id))
                        added = true;
            }
        }

        var filtered = onHand
            .Where(o => variants.ContainsKey(o.VariantId))
            .Select(o =>
            {
                var variant = variants[o.VariantId];
                return new { o.VariantId, variant.ProductName, variant.CategoryId, variant.CategoryName, variant.UnitName, variant.Dimension, o.OnHand, Price = PriceOf(o.VariantId), o.NearestExpiry, variant.ImageKey };
            })
            .Where(o => subtree == null || (o.CategoryId is { } cid && subtree.Contains(cid)))
            .Where(o => string.IsNullOrWhiteSpace(request.Search) || o.ProductName.Contains(request.Search.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderBy(o => o.ProductName)
            .ToList();

        var totalCount = filtered.Count;
        var totalQuantity = filtered.Sum(o => o.OnHand);
        var totalValue = filtered.Sum(o => o.OnHand * o.Price);

        var page = request.Page <= 0 || request.PageSize <= 0
            ? filtered
            : filtered.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();

        var imageKeys = page.Select(o => o.ImageKey).Where(k => k != null).Select(k => k!).Distinct().ToList();
        var imageUrls = await storage.GetUrlsAsync(imageKeys, cancellationToken);

        var items = page
            .Select(o =>
            {
                var imageUrl = o.ImageKey != null && imageUrls.TryGetValue(o.ImageKey, out var u) ? u : null;
                return new StockOnHandDto(o.VariantId, o.ProductName, o.CategoryId, o.CategoryName, o.UnitName, o.Dimension.ToString(), o.OnHand, o.Price, o.NearestExpiry, imageUrl);
            })
            .ToList();

        return new StockOnHandPageDto(items, totalCount, totalQuantity, totalValue);
    }
}
