using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Loyalty;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Stocks.Queries;

public record GetStockOnHandQuery(long WarehouseId, long? CategoryId = null, string? Search = null, int Page = 1, int PageSize = 50)
    : IRequest<StockOnHandPageDto>;

public record StockOnHandDto(long VariantId, string ProductName, long? CategoryId, string? CategoryName, string UnitName, string Dimension, decimal Quantity, decimal SellingPrice, DateOnly? NearestExpiry, string? ImageUrl = null, decimal? DiscountPct = null, string? Code = null);

public record StockOnHandPageDto(IReadOnlyCollection<StockOnHandDto> Items, int TotalCount, decimal TotalQuantity, decimal TotalValue);

public sealed class GetStockOnHandQueryHandler(IApplicationDbContext db, IObjectStorage storage, IFeatureStateProvider features) : IRequestHandler<GetStockOnHandQuery, StockOnHandPageDto>
{
    public async Task<StockOnHandPageDto> Handle(GetStockOnHandQuery request, CancellationToken cancellationToken)
    {
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

        var query = db.Stocks
            .Where(s => s.WarehouseId == request.WarehouseId)
            .GroupBy(s => s.VariantId)
            .Select(g => new { VariantId = g.Key, OnHand = g.Sum(s => s.Quantity), NearestExpiry = g.Min(s => s.ExpiredAt) })
            .Join(db.ProductVariants, o => o.VariantId, v => v.Id, (o, v) => new
            {
                o.VariantId,
                o.OnHand,
                o.NearestExpiry,
                v.ProductId,
                v.Code,
                ProductName = v.Product.Name,
                v.Product.CategoryId,
                v.Product.ManufacturerId,
                CategoryName = v.Product.Category != null ? v.Product.Category.Name : null,
                UnitName = v.Product.Unit.Name,
                Dimension = v.Product.Unit.Dimension,
                ImageKey = v.ImageKey ?? v.Product.ImageKey,
                Price = db.ProductPrices
                        .Where(pp => pp.VariantId == v.Id && pp.WarehouseId == request.WarehouseId)
                        .Select(pp => (decimal?)pp.SellingPrice)
                        .FirstOrDefault()
                    ?? db.ProductPrices
                        .Where(pp => pp.VariantId == v.Id && pp.WarehouseId == null)
                        .Select(pp => (decimal?)pp.SellingPrice)
                        .FirstOrDefault()
                    ?? 0
            });

        if (subtree is not null)
        {
            var ids = subtree.ToList();
            query = query.Where(o => o.CategoryId != null && ids.Contains(o.CategoryId.Value));
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            foreach (var token in request.Search.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var term = $"%{token}%";
                query = query.Where(o => EF.Functions.ILike(o.ProductName, term)
                    || (o.Code != null && EF.Functions.ILike(o.Code, term)));
            }
        }

        var totals = await query
            .GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), Quantity = g.Sum(x => x.OnHand), Value = g.Sum(x => x.OnHand * x.Price) })
            .FirstOrDefaultAsync(cancellationToken);
        if (totals is null)
            return new StockOnHandPageDto([], 0, 0, 0);

        var ordered = query.OrderBy(o => o.ProductName);
        var page = request.Page <= 0 || request.PageSize <= 0
            ? await ordered.ToListAsync(cancellationToken)
            : await ordered.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(cancellationToken);

        var imageKeys = page.Select(o => o.ImageKey).Where(k => k != null).Select(k => k!).Distinct().ToList();
        var imageUrls = await storage.GetUrlsAsync(imageKeys, cancellationToken);

        List<DiscountRule>? rules = null;
        if (page.Count > 0 && await features.IsEnabledAsync(FeatureCatalog.Loyalty, cancellationToken))
            rules = await db.DiscountRules
                .Include(r => r.Exceptions)
                .Where(r => r.IsEnabled && r.CustomerId == null && r.MinAmount == 0 && r.Method == DiscountMethod.Percent)
                .ToListAsync(cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.Now);

        var items = page
            .Select(o =>
            {
                var imageUrl = o.ImageKey != null && imageUrls.TryGetValue(o.ImageKey, out var u) ? u : null;
                var discountPct = rules is { Count: > 0 }
                    ? DiscountEngine.BestPercent(rules, today, o.ProductId, o.CategoryId, o.ManufacturerId)
                    : null;
                return new StockOnHandDto(o.VariantId, o.ProductName, o.CategoryId, o.CategoryName, o.UnitName, o.Dimension.ToString(), o.OnHand, o.Price, o.NearestExpiry, imageUrl, discountPct, o.Code);
            })
            .ToList();

        return new StockOnHandPageDto(items, totals.Count, totals.Quantity, totals.Value);
    }
}
