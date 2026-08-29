using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Cartex.Shared.Models.Products;

namespace Cartex.Application.ProductTypes.Queries;

public record GetProductTypesQuery : IRequest<IReadOnlyCollection<ProductTypeDto>>;

public sealed class GetProductTypesQueryHandler(IApplicationDbContext db) : IRequestHandler<GetProductTypesQuery, IReadOnlyCollection<ProductTypeDto>>
{
    public async Task<IReadOnlyCollection<ProductTypeDto>> Handle(GetProductTypesQuery request, CancellationToken cancellationToken)
    {
        return await db.ProductTypes
            .OrderBy(t => t.Name)
            .Select(t => new ProductTypeDto(t.Id, t.Name, t.TracksExpiry, t.AttributeSchema))
            .ToListAsync(cancellationToken);
    }
}
