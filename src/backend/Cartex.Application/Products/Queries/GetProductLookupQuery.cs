using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Products.Queries;

public record GetProductLookupQuery : IRequest<IReadOnlyCollection<ProductOptionDto>>;

public record ProductOptionDto(long Id, long DefaultVariantId, string Name, string? Dimension, long? UnitId = null, string? UnitShortName = null);

public sealed class GetProductLookupQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetProductLookupQuery, IReadOnlyCollection<ProductOptionDto>>
{
    public async Task<IReadOnlyCollection<ProductOptionDto>> Handle(GetProductLookupQuery request, CancellationToken cancellationToken)
        => await db.Products
            .OrderBy(p => p.Name)
            .Select(p => new ProductOptionDto(
                p.Id,
                p.Variants.Where(v => v.IsDefault).Select(v => v.Id).FirstOrDefault(),
                p.Name,
                p.Unit.Dimension.ToString(),
                p.Unit.Id,
                p.Unit.ShortName))
            .ToListAsync(cancellationToken);
}
