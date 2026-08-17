using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Persistence;
using Cartex.Shared.Models.Products;

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

        query = ProductCatalogSearch.Apply(query, request.Search);
        query = ProductCatalogSearch.ApplyPriceRange(query, request.MinPrice, request.MaxPrice);
        request.Search = null;

        var rows = await query
            .ToPagedListAsync(request,
                p => new
                {
                    p.Id,
                    Variant = p.Variants.Where(v => v.IsDefault).Select(v => new { v.Id, v.Code }).FirstOrDefault(),
                    p.Name,
                    p.CategoryId,
                    CategoryName = p.Category != null ? p.Category.Name : null,
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

        var list = rows
            .Select(r => new ProductDto(
                r.Id,
                r.Variant != null ? r.Variant.Id : 0,
                r.Name,
                r.CategoryName,
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
