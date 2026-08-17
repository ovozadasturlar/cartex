using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Cartex.Shared.Models.Products;

namespace Cartex.Application.Products.Queries;

public record GetProductCategoryCountsQuery : IRequest<IReadOnlyCollection<CategoryCountDto>>;

public sealed class GetProductCategoryCountsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetProductCategoryCountsQuery, IReadOnlyCollection<CategoryCountDto>>
{
    public async Task<IReadOnlyCollection<CategoryCountDto>> Handle(GetProductCategoryCountsQuery request, CancellationToken cancellationToken)
    {
        var rows = await db.Products
            .GroupBy(p => p.Category != null ? p.Category.Name : null)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync(cancellationToken);
        return rows.Select(x => new CategoryCountDto(x.Name, x.Count)).ToList();
    }
}
