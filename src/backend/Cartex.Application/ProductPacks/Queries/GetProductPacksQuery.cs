using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Cartex.Shared.Models.Products;

namespace Cartex.Application.ProductPacks.Queries;

public record GetProductPacksQuery(long ProductId) : IRequest<IReadOnlyCollection<ProductPackDto>>;

public sealed class GetProductPacksQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetProductPacksQuery, IReadOnlyCollection<ProductPackDto>>
{
    public async Task<IReadOnlyCollection<ProductPackDto>> Handle(GetProductPacksQuery request, CancellationToken cancellationToken) =>
        await db.ProductPacks
            .Where(p => p.ProductId == request.ProductId)
            .OrderBy(p => p.Size)
            .Select(p => new ProductPackDto(p.Id, p.ProductId, p.Name, p.Size, p.Kind.ToString(), p.IsDefault))
            .ToListAsync(cancellationToken);
}
