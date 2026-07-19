using Cartex.Application.Common.Models;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Products.Queries;

public record GetProductsTotalsQuery : FilteringRequest, IRequest<ProductsTotalsDto>
{
    public long? CategoryId { get; set; }
}

public record ProductsTotalsDto(int Count, decimal TotalOnHand);

public sealed class GetProductsTotalsQueryHandler(IApplicationDbContext db) : IRequestHandler<GetProductsTotalsQuery, ProductsTotalsDto>
{
    public async Task<ProductsTotalsDto> Handle(GetProductsTotalsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Products.AsQueryable();

        if (request.CategoryId is { } categoryId)
            query = query.Where(p => p.CategoryId == categoryId);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            foreach (var token in request.Search.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var term = $"%{token}%";
                query = query.Where(p =>
                    EF.Functions.ILike(p.Name, term)
                    || (p.IkpuCode != null && EF.Functions.ILike(p.IkpuCode, term))
                    || p.Variants.Any(v => v.Code != null && EF.Functions.ILike(v.Code, term))
                    || p.Variants.Any(v => v.Barcodes.Any(b => EF.Functions.ILike(b.Code, term))));
            }
        }

        return new ProductsTotalsDto(
            await query.CountAsync(cancellationToken),
            await db.Stocks
                .Where(s => query.Any(p => p.Id == s.Variant.ProductId))
                .SumAsync(s => (decimal?)s.Quantity, cancellationToken) ?? 0);
    }
}
