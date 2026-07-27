using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Application.Common.Search;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Products.Queries;

public record GetProductsQuery : FilteringRequest, IRequest<IReadOnlyCollection<ProductDto>>
{
    public long? CategoryId { get; set; }
    public long? VariantId { get; set; }
}

public record ProductDto(
    long Id,
    long DefaultVariantId,
    string Name,
    string? CategoryName,
    string UnitName,
    decimal MinStock,
    List<string> Barcodes,
    long? ProductTypeId,
    string? ProductTypeName,
    bool TracksExpiry,
    string? Attributes,
    string? ImageKey,
    string? Code,
    string? IkpuCode,
    decimal? VatRate,
    decimal? SellingPrice,
    decimal OnHand,
    string? ImageUrl = null,
    string? PriceCurrency = null,
    string? Dimension = null,
    long? ManufacturerId = null,
    bool IsEnabled = true,
    long? CategoryId = null,
    long UnitId = 0);

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

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            foreach (var token in CatalogSearch.Parse(request.Search).Terms)
            {
                var term = $"%{token.Value}%";
                query = token.Field switch
                {
                    CatalogSearchField.Name => query.Where(p => EF.Functions.ILike(p.Name, term)),
                    CatalogSearchField.Barcode => query.Where(p => p.Variants.Any(v => v.Barcodes.Any(b => EF.Functions.ILike(b.Code, term)))),
                    CatalogSearchField.Code => query.Where(p =>
                        (p.IkpuCode != null && EF.Functions.ILike(p.IkpuCode, term))
                        || p.Variants.Any(v => v.Code != null && EF.Functions.ILike(v.Code, term))),
                    CatalogSearchField.Price when token.Price is { } price => query.Where(p => p.Variants.Any(v => v.Prices.Any(x => x.SellingPrice == price))),
                    CatalogSearchField.Price => query.Where(_ => false),
                    _ => query.Where(p =>
                        EF.Functions.ILike(p.Name, term)
                        || (p.IkpuCode != null && EF.Functions.ILike(p.IkpuCode, term))
                        || p.Variants.Any(v => v.Code != null && EF.Functions.ILike(v.Code, term))
                        || p.Variants.Any(v => v.Barcodes.Any(b => EF.Functions.ILike(b.Code, term))))
                };
            }
            request.Search = null;
        }

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
                    // Images can be stored on a variant. Prefer the requested variant's
                    // image (or the default variant's image for a regular product list),
                    // and only then fall back to the product-level image.
                    ImageKey = p.Variants
                        .Where(v => request.VariantId == null ? v.IsDefault : v.Id == request.VariantId)
                        .Select(v => v.ImageKey)
                        .FirstOrDefault() ?? p.ImageKey,
                    p.IkpuCode,
                    p.VatRate,
                    Price = p.Variants.Where(v => v.IsDefault).SelectMany(v => v.Prices).Where(pr => pr.WarehouseId == null).Select(pr => new { pr.SellingPrice, pr.Currency }).FirstOrDefault(),
                    OnHand = p.Variants.SelectMany(v => v.Stocks).Sum(s => s.Quantity),
                    Dimension = p.Unit.Dimension.ToString(),
                    p.ManufacturerId,
                    p.IsEnabled
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
                r.UnitId))
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
