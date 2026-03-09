using MediatR;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;

namespace Cartex.Application.Supplies.Queries;

public record GetSuppliesQuery(long? WarehouseId) : IRequest<List<SupplyDto>>;

public record SupplyDto(long Id, DateOnly SupplyDate, decimal TotalAmount, string SupplierName, string WarehouseName, string UserName);

public sealed class GetSuppliesQueryHandler(IApplicationDbContext db) : IRequestHandler<GetSuppliesQuery, List<SupplyDto>>
{
    public async Task<List<SupplyDto>> Handle(GetSuppliesQuery request, CancellationToken cancellationToken)
    {
        var query = db.Supplies
            .Include(s => s.Supplier)
            .Include(s => s.Warehouse)
            .Include(s => s.User)
            .AsQueryable();

        if (request.WarehouseId is not null)
            query = query.Where(s => s.WarehouseId == request.WarehouseId);

        return await query
            .Select(s => new SupplyDto(
                s.Id,
                s.SupplyDate,
                s.TotalAmount,
                s.Supplier.Name,
                s.Warehouse.Name,
                s.User.FullName))
            .ToListAsync(cancellationToken);
    }
}
