using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Store.Queries;

public record StoreWarehouseDto(long Id, string Name);

public record StoreInfoDto(string BusinessName, string Currency, IReadOnlyList<StoreWarehouseDto> Warehouses);

public record GetStoreInfoQuery : IRequest<StoreInfoDto>;

public sealed class GetStoreInfoQueryHandler(IApplicationDbContext db) : IRequestHandler<GetStoreInfoQuery, StoreInfoDto>
{
    public async Task<StoreInfoDto> Handle(GetStoreInfoQuery request, CancellationToken cancellationToken)
    {
        var business = await db.Businesses.Select(b => new { b.Name, b.Currency }).FirstAsync(cancellationToken);
        var warehouses = await db.Warehouses
            .Where(w => w.IsOnline)
            .OrderBy(w => w.Name)
            .Select(w => new StoreWarehouseDto(w.Id, w.Name))
            .ToListAsync(cancellationToken);
        return new StoreInfoDto(business.Name, business.Currency, warehouses);
    }
}
