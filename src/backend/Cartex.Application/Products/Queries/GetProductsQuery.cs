using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Application.Common.Catalog;
using Cartex.Persistence;
using Cartex.Application.Common.Search;
using Cartex.Shared.Models.Common;
using Cartex.Shared.Models.Products;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Products.Queries;

public record GetProductsQuery : FilteringRequest, IRequest<IReadOnlyCollection<ProductDto>>
{
    public long? CategoryId { get; set; }
    public long? VariantId { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
}

public sealed class GetProductsQueryHandler(
    IApplicationDbContext db,
    IObjectStorage storage,
    IPagingMetadataWriter writer) : IRequestHandler<GetProductsQuery, IReadOnlyCollection<ProductDto>>
{
    public async Task<IReadOnlyCollection<ProductDto>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Products.AsQueryable();

        if (request.CategoryId is { } categoryId)
            query = query.Where(p => p.CategoryId == categoryId);

        if (request.VariantId is { } variantId)
            query = query.Where(p => p.Variants.Any(v => v.Id == variantId));

        var catalogSearch = CatalogSearch.Parse(request.Search);
        query = ProductCatalogSearch.Apply(query, request.Search);
        query = ProductCatalogSearch.ApplyPriceRange(query, request.MinPrice, request.MaxPrice);
        request.Search = null;

        Dictionary<long, int>? relevanceOrder = null;
        var pagingRequest = (FilteringRequest)request;
        var strictNameQuery = ProductCatalogSearch.StrictNameQuery(catalogSearch);
        if (strictNameQuery.Length > 0)
        {
            var filtered = query.AsFilterable(request);
            var candidates = await filtered
                .Select(x => new { x.Id, x.Name })
                .ToListAsync(cancellationToken);
            var total = candidates.Count;
            IEnumerable<long> rankedIds = candidates
                .OrderBy(x => ProductCatalogSearch.NameRank(x.Name, strictNameQuery))
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Id)
                .Select(x => x.Id);

            if (request.Page <= 0 || request.PageSize <= 0)
            {
                if (total > PagingRequest.MaxUnboundedSize)
                    throw new BusinessRuleException($"Natija juda katta ({total}). Iltimos, filtr yoki sahifalashdan foydalaning.");
            }
            else
            {
                var pageSize = Math.Min(request.PageSize, PagingRequest.MaxPageSize);
                rankedIds = rankedIds.Skip((request.Page - 1) * pageSize).Take(pageSize);
                writer.Write(new PagedListMetadata(total, request.Page, pageSize,
                    (int)Math.Ceiling((double)total / pageSize)));
            }

            var pageIds = rankedIds.ToArray();
            relevanceOrder = pageIds.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);
            query = filtered.Where(x => pageIds.Contains(x.Id));
            pagingRequest = new FilteringRequest { Page = 0, PageSize = 0 };
        }

        var rows = await query
            .ToPagedListAsync(pagingRequest,
                p => new
                {
                    p.Id,
                    Variant = p.Variants.Where(v => v.IsDefault).Select(v => new { v.Id, v.Code }).FirstOrDefault(),
                    p.Name,
                    p.CategoryId,
                    p.UnitId,
                    UnitName = p.Unit.Name,
                    p.MinStock,
                    Barcodes = p.Variants.SelectMany(v => v.Barcodes).Select(b => b.Code).ToList(),
                    p.ProductTypeId,
                    ProductTypeName = p.ProductType != null ? p.ProductType.Name : null,
                    TracksExpiry = p.TracksExpiryOverride ?? (p.ProductType != null && p.ProductType.TracksExpiry),
                    p.Attributes,
                    ImageKey = p.Variants
                        .Where(v => request.VariantId == null ? v.IsDefault : v.Id == request.VariantId)
                        .Select(v => v.ImageKey)
                        .FirstOrDefault() ?? p.ImageKey,
                    p.IkpuCode,
                    p.VatRate,
                    Price = p.Variants.Where(v => v.IsDefault).SelectMany(v => v.Prices).Where(pr => pr.WarehouseId == null).Select(pr => new
                    {
                        pr.SellingPrice,
                        pr.Currency,
                        Symbol = db.Currencies.Where(c => c.Code == pr.Currency).Select(c => c.Symbol).FirstOrDefault(),
                        SymbolPosition = db.Currencies.Where(c => c.Code == pr.Currency).Select(c => c.SymbolPosition).FirstOrDefault(),
                        DecimalDigits = db.Currencies.Where(c => c.Code == pr.Currency).Select(c => (int?)c.DecimalDigits).FirstOrDefault()
                    }).FirstOrDefault(),
                    OnHand = p.Variants.SelectMany(v => v.Stocks).Sum(s => s.Quantity),
                    Dimension = p.Unit.Dimension.ToString(),
                    p.ManufacturerId,
                    p.IsEnabled,
                    p.AmountEntryEnabled,
                    p.FractionalOverride,
                    p.Unit.AllowFractional,
                    p.Unit.DefaultAllowAmountEntry
                },
                writer, cancellationToken);

        if (relevanceOrder is not null)
            rows = rows.OrderBy(x => relevanceOrder[x.Id]).ToList();

        var categoryPaths = await CategoryPathLookup.LoadAsync(db, cancellationToken);
        var list = rows
            .Select(r => new ProductDto(
                r.Id,
                r.Variant != null ? r.Variant.Id : 0,
                r.Name,
                r.CategoryId is { } categoryId ? categoryPaths.GetValueOrDefault(categoryId) : null,
                r.UnitName,
                r.MinStock,
                r.Barcodes,
                r.ProductTypeId,
                r.ProductTypeName,
                r.TracksExpiry,
                r.Attributes,
                r.ImageKey,
                r.Variant?.Code,
                r.IkpuCode,
                r.VatRate,
                r.Price?.SellingPrice,
                r.OnHand,
                null,
                r.Price?.Currency,
                r.Dimension,
                r.ManufacturerId,
                r.IsEnabled,
                r.CategoryId,
                r.UnitId,
                r.AmountEntryEnabled ?? r.DefaultAllowAmountEntry,
                r.Price?.Symbol,
                r.Price?.SymbolPosition,
                r.Price?.DecimalDigits,
                r.FractionalOverride ?? r.AllowFractional,
                r.FractionalOverride,
                r.AmountEntryEnabled))
            .ToList();

        var keys = list.Where(p => p.ImageKey != null).Select(p => p.ImageKey!).Distinct().ToList();
        if (keys.Count == 0)
            return list;

        var urls = await storage.GetUrlsAsync(keys, cancellationToken);
        return list
            .Select(p => p.ImageKey != null && urls.TryGetValue(p.ImageKey, out var u) ? p with { ImageUrl = u } : p)
            .ToList();
    }

}
