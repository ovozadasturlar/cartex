using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Products.Queries;

public record GetProductsQuery : FilteringRequest, IRequest<IReadOnlyCollection<ProductDto>>;

public record ProductDto(
    long Id,
    string Name,
    string? CategoryName,
    string UnitName,
    decimal MinStock,
    List<string> Barcodes,
    long? ProductTypeId,
    string? ProductTypeName,
    bool TracksExpiry,
    string? MeasureMode,
    string? Attributes);

public sealed class GetProductsQueryHandler(
    IApplicationDbContext db,
    IPagingMetadataWriter writer) : IRequestHandler<GetProductsQuery, IReadOnlyCollection<ProductDto>>
{
    public async Task<IReadOnlyCollection<ProductDto>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        return await db.Products
            .Include(p => p.Category)
            .Include(p => p.Unit)
            .Include(p => p.ProductType)
            .Include(p => p.Barcodes)
            .ToPagedListAsync(request,
                p => new ProductDto(
                    p.Id,
                    p.Name,
                    p.Category != null ? p.Category.Name : null,
                    p.Unit.Name,
                    p.MinStock,
                    p.Barcodes.Select(b => b.Code).ToList(),
                    p.ProductTypeId,
                    p.ProductType != null ? p.ProductType.Name : null,
                    p.TracksExpiryOverride ?? (p.ProductType != null && p.ProductType.TracksExpiry),
                    p.ProductType != null ? p.ProductType.MeasureMode.ToString() : null,
                    p.Attributes),
                writer, cancellationToken);
    }
}
