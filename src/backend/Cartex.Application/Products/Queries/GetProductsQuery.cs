using MediatR;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;

namespace Cartex.Application.Products.Queries;

public record GetProductsQuery(long? CategoryId, string? Search) : IRequest<List<ProductDto>>;

public record ProductDto(long Id, string Name, string? CategoryName, string UnitName, decimal MinStock, List<string> Barcodes);

public sealed class GetProductsQueryHandler(IApplicationDbContext db) : IRequestHandler<GetProductsQuery, List<ProductDto>>
{
    public async Task<List<ProductDto>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Products
            .Include(p => p.Category)
            .Include(p => p.Unit)
            .Include(p => p.Barcodes)
            .AsQueryable();

        if (request.CategoryId is not null)
            query = query.Where(p => p.CategoryId == request.CategoryId);

        if (request.Search is not null)
            query = query.Where(p => p.Name.Contains(request.Search));

        return await query
            .Select(p => new ProductDto(
                p.Id,
                p.Name,
                p.Category != null ? p.Category.Name : null,
                p.Unit.Name,
                p.MinStock,
                p.Barcodes.Select(b => b.Code).ToList()))
            .ToListAsync(cancellationToken);
    }
}
