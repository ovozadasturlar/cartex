using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Store.Queries;

public record StoreCatalogItemDto(long VariantId, string Name, string? VariantName, string Unit, decimal Price, bool Available);

public record GetStoreCatalogQuery(long WarehouseId, string? Search = null, int Page = 1, int PageSize = 50)
    : IRequest<IReadOnlyList<StoreCatalogItemDto>>;

public sealed class GetStoreCatalogQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetStoreCatalogQuery, IReadOnlyList<StoreCatalogItemDto>>
{
    public async Task<IReadOnlyList<StoreCatalogItemDto>> Handle(GetStoreCatalogQuery request, CancellationToken cancellationToken)
    {
        var online = await db.Warehouses.AnyAsync(w => w.Id == request.WarehouseId && w.IsOnline, cancellationToken);
        if (!online)
            throw new NotFoundException("Do'kon topilmadi.");

        var baseCode = await db.Businesses.Select(b => b.Currency).FirstAsync(cancellationToken);

        var query = db.ProductVariants
            .Where(v => v.Prices.Any(p => (p.WarehouseId == request.WarehouseId || p.WarehouseId == null) && p.Currency == baseCode));

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = $"%{request.Search.Trim()}%";
            query = query.Where(v => EF.Functions.ILike(v.Product.Name, term));
        }

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        return await query
            .OrderBy(v => v.Product.Name).ThenBy(v => v.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(v => new StoreCatalogItemDto(
                v.Id,
                v.Product.Name,
                v.Name,
                v.Product.Unit.Name,
                v.Prices
                    .Where(p => (p.WarehouseId == request.WarehouseId || p.WarehouseId == null) && p.Currency == baseCode)
                    .OrderBy(p => p.WarehouseId == null)
                    .Select(p => p.SellingPrice)
                    .FirstOrDefault(),
                v.Stocks.Any(s => s.WarehouseId == request.WarehouseId && s.Quantity > 0)))
            .ToListAsync(cancellationToken);
    }
}
