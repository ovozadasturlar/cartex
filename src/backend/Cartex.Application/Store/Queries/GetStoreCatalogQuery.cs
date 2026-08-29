using Cartex.Persistence;
using Cartex.Shared.Search;
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
            var value = request.Search.Trim();
            var term = $"%{value}%";
            var folded = SearchFold.Fuzzy(value);
            var foldedTerm = $"%{folded}%";
            query = query.Where(v => EF.Functions.ILike(v.Product.Name, term)
                || (folded.Length > 0 && v.Product.SearchFold != null && EF.Functions.ILike(v.Product.SearchFold, foldedTerm)));
        }

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var pageQuery = query;
        Dictionary<long, int>? relevanceOrder = null;
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var strictQuery = SearchFold.Strict(request.Search);
            var candidates = await query.Select(v => new { v.Id, v.Product.Name }).ToListAsync(cancellationToken);
            var pageIds = candidates
                .OrderBy(x => NameRank(x.Name, strictQuery))
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => x.Id)
                .ToArray();
            relevanceOrder = pageIds.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);
            pageQuery = query.Where(v => pageIds.Contains(v.Id));
        }
        else
        {
            pageQuery = query.OrderBy(v => v.Product.Name).ThenBy(v => v.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize);
        }

        var items = await pageQuery
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
        return relevanceOrder is null
            ? items
            : items.OrderBy(x => relevanceOrder[x.VariantId]).ToList();
    }

    private static int NameRank(string name, string strictQuery)
    {
        var strictName = SearchFold.Strict(name);
        if (strictName.StartsWith(strictQuery, StringComparison.Ordinal))
            return 0;
        return strictName.Contains(strictQuery, StringComparison.Ordinal) ? 1 : 2;
    }
}
