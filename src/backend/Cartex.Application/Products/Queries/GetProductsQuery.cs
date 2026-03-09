using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Products.Queries;

public record GetProductsQuery : FilteringRequest, IRequest<IReadOnlyCollection<ProductDto>>;

public record ProductDto(long Id, string Name, string? CategoryName, string UnitName, decimal MinStock, List<string> Barcodes);

public sealed class GetProductsQueryHandler(
    IApplicationDbContext db,
    IPagingMetadataWriter writer) : IRequestHandler<GetProductsQuery, IReadOnlyCollection<ProductDto>>
{
    public async Task<IReadOnlyCollection<ProductDto>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        return await db.Products
            .Include(p => p.Category)
            .Include(p => p.Unit)
            .Include(p => p.Barcodes)
            .Select(p => new ProductDto(
                p.Id,
                p.Name,
                p.Category != null ? p.Category.Name : null,
                p.Unit.Name,
                p.MinStock,
                p.Barcodes.Select(b => b.Code).ToList()))
            .ToPagedListAsync(request, writer, cancellationToken);
    }
}
