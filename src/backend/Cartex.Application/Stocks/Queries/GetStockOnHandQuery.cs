using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Loyalty;
using Cartex.Application.Common.Finance;
using Cartex.Application.Common.Settings;
using Cartex.Application.Common.Search;
using Cartex.Persistence;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Enums;
using Cartex.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Stocks.Queries;

public record GetStockOnHandQuery(long WarehouseId, long? CategoryId = null, string? Search = null, int Page = 1, int PageSize = 50, bool ForSale = false)
    : IRequest<StockOnHandPageDto>;

public record StockOnHandDto(long VariantId, string ProductName, long? CategoryId, string? CategoryName, string UnitName, string Dimension, decimal Quantity, decimal SellingPrice, DateOnly? NearestExpiry, string? ImageUrl = null, decimal? DiscountPct = null, string? Code = null, List<string>? Barcodes = null, bool AllowsAmountEntry = false, bool AllowsFractional = false);

public record StockOnHandPageDto(IReadOnlyCollection<StockOnHandDto> Items, int TotalCount, decimal TotalQuantity, decimal TotalValue);

public sealed class GetStockOnHandQueryHandler(
    IApplicationDbContext db,
    IObjectStorage storage,
    IFeatureStateProvider features,
    ISettingsService settings,
    ICurrencyService currency) : IRequestHandler<GetStockOnHandQuery, StockOnHandPageDto>
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

        var stockTotals = db.Stocks
            .Where(s => s.WarehouseId == request.WarehouseId)
            .GroupBy(s => s.VariantId)
            .Select(g => new { VariantId = g.Key, OnHand = g.Sum(s => s.Quantity), NearestExpiry = g.Min(s => s.ExpiredAt) });

        var branchId = 0L;
        var policy = new SalesPolicySettings();
        long[] activeVariantIds = [];
        long[] forceVisibleVariantIds = [];
        long[] forceHiddenVariantIds = [];
        if (request.ForSale)
        {
            branchId = await db.Warehouses
                .Where(x => x.Id == request.WarehouseId)
                .Select(x => x.BranchId)
                .FirstOrDefaultAsync(cancellationToken);
            if (branchId == 0)
                throw new NotFoundException("Warehouse not found.");
            policy = await settings.GetAsync<SalesPolicySettings>(SettingKeys.SalesPolicy, cancellationToken) ?? new SalesPolicySettings();

            var catalog = await db.BranchCatalogEntries
                .Where(x => x.BranchId == branchId)
                .Select(x => new { x.VariantId, x.FirstActivityAt, x.VisibilityOverride })
                .ToListAsync(cancellationToken);
            activeVariantIds = catalog.Where(x => x.FirstActivityAt != null).Select(x => x.VariantId).ToArray();
            forceVisibleVariantIds = catalog.Where(x => x.VisibilityOverride == BranchCatalogVisibilityOverride.ForceVisible).Select(x => x.VariantId).ToArray();
            forceHiddenVariantIds = catalog.Where(x => x.VisibilityOverride == BranchCatalogVisibilityOverride.ForceHidden).Select(x => x.VariantId).ToArray();
        }

        var baseCurrency = await currency.BaseAsync(cancellationToken);
        var priceCurrencies = await db.ProductPrices
            .Where(p => p.WarehouseId == request.WarehouseId || p.WarehouseId == null)
            .Select(p => p.Currency)
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (var code in priceCurrencies.Where(code => code != baseCurrency))
            await currency.RateAsync(code, cancellationToken);

        var variantQuery = db.ProductVariants.AsNoTracking();

        if (subtree is not null)
        {
            var ids = subtree.ToList();
            variantQuery = variantQuery.Where(v => v.Product.CategoryId != null && ids.Contains(v.Product.CategoryId.Value));
        }

        var search = CatalogSearch.Parse(request.Search);
        if (search.Terms.Count > 0 && search.Terms.All(t => t.Field != CatalogSearchField.Price))
        {
            var terms = search.Terms.OrderByDescending(t => t.Value.Length).ToList();
            for (var index = 0; index < terms.Count; index++)
            {
                variantQuery = ApplySearch(variantQuery, terms[index]);
                if (index == terms.Count - 1)
                    break;

                var narrowedIds = await variantQuery.Select(v => v.Id).ToArrayAsync(cancellationToken);
                if (narrowedIds.Length == 0)
                    return new StockOnHandPageDto([], 0, 0, 0);
                variantQuery = db.ProductVariants.AsNoTracking().Where(v => narrowedIds.Contains(v.Id));
            }
        }

        var query = variantQuery
            .Select(v => new
            {
                VariantId = v.Id,
                v.ProductId,
                v.Code,
                ProductName = v.Product.Name,
                v.Product.CategoryId,
                v.Product.ManufacturerId,
                v.Product.IsEnabled,
                CategoryName = v.Product.Category == null ? null : v.Product.Category!.Name,
                UnitName = v.Product.Unit.Name,
                Dimension = v.Product.Unit.Dimension,
                v.Product.AmountEntryEnabled,
                v.Product.FractionalOverride,
                v.Product.Unit.AllowFractional,
                v.Product.Unit.DefaultAllowAmountEntry,
                ImageKey = v.ImageKey ?? v.Product.ImageKey,
                Price = db.ProductPrices
                    .Where(pp => pp.VariantId == v.Id && (pp.WarehouseId == request.WarehouseId || pp.WarehouseId == null))
                    .OrderByDescending(pp => pp.WarehouseId == request.WarehouseId)
                    .Select(pp => Math.Round(pp.SellingPrice * (pp.Currency == baseCurrency
                        ? 1m
                        : db.ExchangeRates
                            .Where(rate => rate.Code == pp.Currency)
                            .OrderByDescending(rate => rate.EffectiveAt)
                            .Select(rate => rate.Rate)
                            .First()), 2))
                    .FirstOrDefault(),
            });

        if (search.Terms.Any(t => t.Field == CatalogSearchField.Price))
        {
            foreach (var token in search.Terms)
            {
                var term = $"%{token.Value}%";
                query = token.Field switch
                {
                    CatalogSearchField.Name => query.Where(o => EF.Functions.ILike(o.ProductName, term)),
                    CatalogSearchField.Barcode => query.Where(o => db.Barcodes.Any(b => b.VariantId == o.VariantId && EF.Functions.ILike(b.Code, term))),
                    CatalogSearchField.Code => query.Where(o => o.Code != null && EF.Functions.ILike(o.Code, term)),
                    CatalogSearchField.Price when token.Price is { } price => query.Where(o => o.Price == price),
                    CatalogSearchField.Price => query.Where(_ => false),
                    _ => query.Where(o => EF.Functions.ILike(o.ProductName, term)
                        || (o.Code != null && EF.Functions.ILike(o.Code, term))
                        || db.Barcodes.Any(b => b.VariantId == o.VariantId && EF.Functions.ILike(b.Code, term)))
                };
            }
        }

        if (request.ForSale)
        {
            query = query.Where(o => o.IsEnabled);
            if (forceHiddenVariantIds.Length > 0)
                query = query.Where(o => !forceHiddenVariantIds.Contains(o.VariantId));
            if (!policy.ShowUnlistedProducts)
            {
                var visibleVariantIds = activeVariantIds.Concat(forceVisibleVariantIds).Distinct().ToArray();
                query = visibleVariantIds.Length == 0
                    ? query.Where(_ => false)
                    : query.Where(o => visibleVariantIds.Contains(o.VariantId));
            }
            if (!policy.ShowOutOfStock)
            {
                var inStockVariantIds = await stockTotals
                    .Where(x => x.OnHand > 0)
                    .Select(x => x.VariantId)
                    .ToArrayAsync(cancellationToken);
                var visibleVariantIds = inStockVariantIds.Concat(forceVisibleVariantIds).Distinct().ToArray();
                query = visibleVariantIds.Length == 0
                    ? query.Where(_ => false)
                    : query.Where(o => visibleVariantIds.Contains(o.VariantId));
            }
        }
        else
        {
            var stockedVariantIds = await stockTotals.Select(x => x.VariantId).ToArrayAsync(cancellationToken);
            query = stockedVariantIds.Length == 0
                ? query.Where(_ => false)
                : query.Where(o => stockedVariantIds.Contains(o.VariantId));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        if (totalCount == 0)
            return new StockOnHandPageDto([], 0, 0, 0);

        var totals = await (
                from stock in stockTotals
                join product in query on stock.VariantId equals product.VariantId
                select new { stock.OnHand, product.Price })
            .GroupBy(_ => 1)
            .Select(g => new { Quantity = g.Sum(x => x.OnHand), Value = g.Sum(x => x.OnHand * x.Price) })
            .FirstOrDefaultAsync(cancellationToken);

        var ordered = query.OrderBy(o => o.ProductName);
        var page = request.Page <= 0 || request.PageSize <= 0
            ? await ordered.ToListAsync(cancellationToken)
            : await ordered.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(cancellationToken);

        var pageVariantIds = page.Select(o => o.VariantId).ToArray();
        var stockByVariant = await stockTotals
            .Where(s => pageVariantIds.Contains(s.VariantId))
            .ToDictionaryAsync(s => s.VariantId, cancellationToken);
        var barcodeRows = await db.Barcodes
            .Where(b => pageVariantIds.Contains(b.VariantId))
            .Select(b => new { b.VariantId, b.Code })
            .ToListAsync(cancellationToken);
        var barcodesByVariant = barcodeRows
            .GroupBy(b => b.VariantId)
            .ToDictionary(g => g.Key, g => g.Select(b => b.Code).ToList());

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
                var stock = stockByVariant.GetValueOrDefault(o.VariantId);
                var imageUrl = o.ImageKey != null && imageUrls.TryGetValue(o.ImageKey, out var u) ? u : null;
                var discountPct = rules is { Count: > 0 }
                    ? DiscountEngine.BestPercent(rules, today, o.ProductId, o.CategoryId, o.ManufacturerId)
                    : null;
                var barcodes = barcodesByVariant.TryGetValue(o.VariantId, out var values) ? values : [];
                var allowsAmountEntry = o.AmountEntryEnabled ?? o.DefaultAllowAmountEntry;
                var allowsFractional = o.FractionalOverride ?? o.AllowFractional;
                return new StockOnHandDto(o.VariantId, o.ProductName, o.CategoryId, o.CategoryName, o.UnitName, o.Dimension.ToString(), stock?.OnHand ?? 0m, o.Price, stock?.NearestExpiry, imageUrl, discountPct, o.Code, barcodes, allowsAmountEntry, allowsFractional);
            })
            .ToList();

        return new StockOnHandPageDto(items, totalCount, totals?.Quantity ?? 0m, totals?.Value ?? 0m);
    }

    private static IQueryable<ProductVariant> ApplySearch(IQueryable<ProductVariant> query, CatalogSearchTerm token)
    {
        var term = $"%{token.Value}%";
        return token.Field switch
        {
            CatalogSearchField.Name => query.Where(v => EF.Functions.ILike(v.Product.Name, term)),
            CatalogSearchField.Barcode => query.Where(v => v.Barcodes.Any(b => EF.Functions.ILike(b.Code, term))),
            CatalogSearchField.Code => query.Where(v => v.Code != null && EF.Functions.ILike(v.Code, term)),
            _ => query.Where(v => EF.Functions.ILike(v.Product.Name, term)
                || (v.Code != null && EF.Functions.ILike(v.Code, term))
                || v.Barcodes.Any(b => EF.Functions.ILike(b.Code, term)))
        };
    }
}
