using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Products.Queries;

public record GetProductsQuery : FilteringRequest, IRequest<IReadOnlyCollection<ProductDto>>
{
    public long? CategoryId { get; set; }
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
    string? Dimension = null);

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

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = $"%{request.Search.Trim()}%";
            query = query.Where(p =>
                EF.Functions.ILike(p.Name, term)
                || (p.IkpuCode != null && EF.Functions.ILike(p.IkpuCode, term))
                || p.Variants.Any(v => v.Code != null && EF.Functions.ILike(v.Code, term))
                || p.Variants.Any(v => v.Barcodes.Any(b => EF.Functions.ILike(b.Code, term))));
            request.Search = null;
        }

        var list = await query
            .ToPagedListAsync(request,
                p => new ProductDto(
                    p.Id,
                    p.Variants.Where(v => v.IsDefault).Select(v => v.Id).FirstOrDefault(),
                    p.Name,
                    p.Category != null ? p.Category.Name : null,
                    p.Unit.Name,
                    p.MinStock,
                    p.Variants.SelectMany(v => v.Barcodes).Select(b => b.Code).ToList(),
                    p.ProductTypeId,
                    p.ProductType != null ? p.ProductType.Name : null,
                    p.TracksExpiryOverride ?? (p.ProductType != null && p.ProductType.TracksExpiry),
                    p.Attributes,
                    p.ImageKey,
                    p.Variants.Where(v => v.IsDefault).Select(v => v.Code).FirstOrDefault(),
                    p.IkpuCode,
                    p.VatRate,
                    p.Variants.Where(v => v.IsDefault).SelectMany(v => v.Prices).Where(pr => pr.WarehouseId == null).Select(pr => (decimal?)pr.SellingPrice).FirstOrDefault(),
                    p.Variants.SelectMany(v => v.Stocks).Sum(s => s.Quantity),
                    null,
                    p.Variants.Where(v => v.IsDefault).SelectMany(v => v.Prices).Where(pr => pr.WarehouseId == null).Select(pr => pr.Currency).FirstOrDefault(),
                    p.Unit.Dimension.ToString()),
                writer, cancellationToken);

        var keys = list.Where(p => p.ImageKey != null).Select(p => p.ImageKey!).Distinct().ToList();
        if (keys.Count == 0)
            return list;

        var urls = await storage.GetUrlsAsync(keys, cancellationToken);
        return list
            .Select(p => p.ImageKey != null && urls.TryGetValue(p.ImageKey, out var u) ? p with { ImageUrl = u } : p)
            .ToList();
    }
}
