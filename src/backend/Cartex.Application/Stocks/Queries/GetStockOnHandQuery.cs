using Cartex.Application.Common.Catalog;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Loyalty;
using Cartex.Application.Common.Finance;
using Cartex.Application.Common.Settings;
using Cartex.Application.Common.Search;
using Cartex.Application.Products.Queries;
using Cartex.Persistence;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Enums;
using Cartex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Cartex.Shared.Models.Stocks;
using Cartex.Shared.Search;

namespace Cartex.Application.Stocks.Queries;

public record GetStockOnHandQuery(long WarehouseId, long? CategoryId = null, string? Search = null, int Page = 1, int PageSize = 50, bool ForSale = false, IReadOnlyCollection<long>? VariantIds = null)
    : IRequest<StockOnHandPageDto>;

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

        var baseCurrency = await currency.BaseAsync(cancellationToken);
        var priceCurrencies = await db.ProductPrices
            .Where(p => p.WarehouseId == request.WarehouseId || p.WarehouseId == null)
            .Select(p => p.Currency)
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (var code in priceCurrencies.Where(code => code != baseCurrency))
            await currency.RateAsync(code, cancellationToken);

        var variantQuery = db.ProductVariants.AsNoTracking();

        if (request.VariantIds is { } requested)
        {
            var ids = requested as long[] ?? [.. requested];
            variantQuery = ids.Length == 0
                ? variantQuery.Where(_ => false)
                : variantQuery.Where(v => ids.Contains(v.Id));
        }

        if (request.ForSale)
        {
            var branchId = await db.Warehouses
                .Where(x => x.Id == request.WarehouseId)
                .Select(x => x.BranchId)
                .FirstOrDefaultAsync(cancellationToken);
            if (branchId == 0)
                throw new NotFoundException("Warehouse not found.");
            var policy = await settings.GetAsync<SalesPolicySettings>(SettingKeys.SalesPolicy, cancellationToken) ?? new SalesPolicySettings();
            variantQuery = await CatalogVisibility.ForSaleAsync(
                variantQuery, db, policy, branchId, request.WarehouseId, cancellationToken);
        }

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
                ProductSearchFold = v.Product.SearchFold,
                v.Product.CategoryId,
                v.Product.ManufacturerId,
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
                var folded = SearchFold.Fuzzy(token.Value);
                var foldedTerm = $"%{folded}%";
                query = token.Field switch
                {
                    CatalogSearchField.Name => query.Where(o => EF.Functions.ILike(o.ProductName, term)
                        || (folded.Length > 0 && o.ProductSearchFold != null && EF.Functions.ILike(o.ProductSearchFold, foldedTerm))),
                    CatalogSearchField.Barcode => query.Where(o => db.Barcodes.Any(b => b.VariantId == o.VariantId && EF.Functions.ILike(b.Code, term))),
                    CatalogSearchField.Code => query.Where(o => o.Code != null && EF.Functions.ILike(o.Code, term)),
                    CatalogSearchField.Price when token.Price is { } price => query.Where(o => o.Price == price),
                    CatalogSearchField.Price => query.Where(_ => false),
                    _ => query.Where(o => EF.Functions.ILike(o.ProductName, term)
                        || (folded.Length > 0 && o.ProductSearchFold != null && EF.Functions.ILike(o.ProductSearchFold, foldedTerm))
                        || (o.Code != null && EF.Functions.ILike(o.Code, term))
                        || db.Barcodes.Any(b => b.VariantId == o.VariantId && EF.Functions.ILike(b.Code, term)))
                };
            }
        }

        if (!request.ForSale)
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

        var strictNameQuery = ProductCatalogSearch.StrictNameQuery(search);
        var pageQuery = query;
        Dictionary<long, int>? relevanceOrder = null;
        if (strictNameQuery.Length > 0)
        {
            var candidates = await query
                .Select(x => new { x.VariantId, x.ProductName })
                .ToListAsync(cancellationToken);
            var rankedIds = candidates
                .OrderBy(x => ProductCatalogSearch.NameRank(x.ProductName, strictNameQuery))
                .ThenBy(x => x.ProductName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.VariantId)
                .Select(x => x.VariantId);
            if (request.Page > 0 && request.PageSize > 0)
                rankedIds = rankedIds.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize);
            var pageIds = rankedIds.ToArray();
            relevanceOrder = pageIds.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);
            pageQuery = query.Where(x => pageIds.Contains(x.VariantId));
        }
        else
        {
            pageQuery = query.OrderBy(x => x.ProductName).ThenBy(x => x.VariantId);
            if (request.Page > 0 && request.PageSize > 0)
                pageQuery = pageQuery.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize);
        }

        var page = await pageQuery.ToListAsync(cancellationToken);
        if (relevanceOrder is not null)
            page = page.OrderBy(x => relevanceOrder[x.VariantId]).ToList();

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

        var categoryPaths = await CategoryPathLookup.LoadAsync(db, cancellationToken);
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
                var categoryPath = o.CategoryId is { } categoryId ? categoryPaths.GetValueOrDefault(categoryId) : null;
                return new StockOnHandDto(o.VariantId, o.ProductName, o.CategoryId, categoryPath, o.UnitName, o.Dimension.ToString(), stock?.OnHand ?? 0m, o.Price, stock?.NearestExpiry, imageUrl, discountPct, o.Code, barcodes, allowsAmountEntry, allowsFractional);
            })
            .ToList();

        return new StockOnHandPageDto(items, totalCount, totals?.Quantity ?? 0m, totals?.Value ?? 0m);
    }

    private static IQueryable<ProductVariant> ApplySearch(IQueryable<ProductVariant> query, CatalogSearchTerm token)
    {
        var term = $"%{token.Value}%";
        var folded = SearchFold.Fuzzy(token.Value);
        var foldedTerm = $"%{folded}%";
        return token.Field switch
        {
            CatalogSearchField.Name => query.Where(v => EF.Functions.ILike(v.Product.Name, term)
                || (folded.Length > 0 && v.Product.SearchFold != null && EF.Functions.ILike(v.Product.SearchFold, foldedTerm))),
            CatalogSearchField.Barcode => query.Where(v => v.Barcodes.Any(b => EF.Functions.ILike(b.Code, term))),
            CatalogSearchField.Code => query.Where(v => v.Code != null && EF.Functions.ILike(v.Code, term)),
            _ => query.Where(v => EF.Functions.ILike(v.Product.Name, term)
                || (folded.Length > 0 && v.Product.SearchFold != null && EF.Functions.ILike(v.Product.SearchFold, foldedTerm))
                || (v.Code != null && EF.Functions.ILike(v.Code, term))
                || v.Barcodes.Any(b => EF.Functions.ILike(b.Code, term)))
        };
    }
}
