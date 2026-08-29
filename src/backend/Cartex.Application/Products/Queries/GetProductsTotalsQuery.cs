using Cartex.Application.Common.Models;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Cartex.Shared.Models.Products;

namespace Cartex.Application.Products.Queries;

public record GetProductsTotalsQuery : FilteringRequest, IRequest<ProductsTotalsDto>
{
    public long? CategoryId { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
}

public sealed class GetProductsTotalsQueryHandler(IApplicationDbContext db) : IRequestHandler<GetProductsTotalsQuery, ProductsTotalsDto>
{
    public async Task<ProductsTotalsDto> Handle(GetProductsTotalsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Products.AsQueryable();

        if (request.CategoryId is { } categoryId)
            query = query.Where(p => p.CategoryId == categoryId);

        query = ProductCatalogSearch.Apply(query, request.Search);
        query = ProductCatalogSearch.ApplyPriceRange(query, request.MinPrice, request.MaxPrice);

        return new ProductsTotalsDto(
            await query.CountAsync(cancellationToken),
            await db.Stocks
                .Where(s => query.Any(p => p.Id == s.Variant.ProductId))
                .SumAsync(s => (decimal?)s.Quantity, cancellationToken) ?? 0);
    }
}
