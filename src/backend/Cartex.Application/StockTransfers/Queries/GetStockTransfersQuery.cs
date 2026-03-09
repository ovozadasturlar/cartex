using MediatR;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;

namespace Cartex.Application.StockTransfers.Queries;

public record GetStockTransfersQuery(long? WarehouseId) : IRequest<List<StockTransferDto>>;

public record StockTransferDto(
    long Id,
    string ProductName,
    decimal Quantity,
    string FromWarehouse,
    string ToWarehouse,
    string Status,
    DateTime CreatedAt,
    string UserName);

public sealed class GetStockTransfersQueryHandler(IApplicationDbContext db) : IRequestHandler<GetStockTransfersQuery, List<StockTransferDto>>
{
    public async Task<List<StockTransferDto>> Handle(GetStockTransfersQuery request, CancellationToken cancellationToken)
    {
        var query = db.StockTransfers
            .Include(t => t.Product)
            .Include(t => t.FromWarehouse)
            .Include(t => t.ToWarehouse)
            .Include(t => t.User)
            .AsQueryable();

        if (request.WarehouseId is not null)
            query = query.Where(t => t.FromWarehouseId == request.WarehouseId || t.ToWarehouseId == request.WarehouseId);

        return await query
            .Select(t => new StockTransferDto(
                t.Id,
                t.Product.Name,
                t.Quantity,
                t.FromWarehouse.Name,
                t.ToWarehouse.Name,
                t.Status.ToString(),
                t.CreatedAt,
                t.User.FullName))
            .ToListAsync(cancellationToken);
    }
}
