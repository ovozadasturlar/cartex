using Cartex.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.ProductTypes.Queries;

public record GetProductTypesQuery : IRequest<IReadOnlyCollection<ProductTypeDto>>;

public record ProductTypeDto(long Id, string Name, bool TracksExpiry, string MeasureMode);

public sealed class GetProductTypesQueryHandler(IApplicationDbContext db) : IRequestHandler<GetProductTypesQuery, IReadOnlyCollection<ProductTypeDto>>
{
    public async Task<IReadOnlyCollection<ProductTypeDto>> Handle(GetProductTypesQuery request, CancellationToken cancellationToken)
    {
        return await db.ProductTypes
            .OrderBy(t => t.Name)
            .Select(t => new ProductTypeDto(t.Id, t.Name, t.TracksExpiry, t.MeasureMode.ToString()))
            .ToListAsync(cancellationToken);
    }
}
