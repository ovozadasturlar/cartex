using MediatR;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;

namespace Cartex.Application.Warehouses.Queries;

public record GetWarehousesQuery(long? ShopId) : IRequest<List<WarehouseDto>>;

public record WarehouseDto(long Id, string Name, long ShopId, string ShopName);

public sealed class GetWarehousesQueryHandler(IApplicationDbContext db) : IRequestHandler<GetWarehousesQuery, List<WarehouseDto>>
{
    public async Task<List<WarehouseDto>> Handle(GetWarehousesQuery request, CancellationToken cancellationToken)
    {
        var query = db.Warehouses
            .Include(w => w.Shop)
            .AsQueryable();

        if (request.ShopId is not null)
            query = query.Where(w => w.ShopId == request.ShopId);

        return await query
            .Select(w => new WarehouseDto(w.Id, w.Name, w.ShopId, w.Shop.Name))
            .ToListAsync(cancellationToken);
    }
}
