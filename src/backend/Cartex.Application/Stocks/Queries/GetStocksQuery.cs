using MediatR;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;

namespace Cartex.Application.Stocks.Queries;

public record GetStocksQuery(long WarehouseId, string? Search) : IRequest<List<StockDto>>;

public record StockDto(long Id, string ProductName, string UnitName, decimal Quantity, decimal PurchasePrice, decimal SellingPrice, DateOnly? ExpiredAt);

public sealed class GetStocksQueryHandler(IApplicationDbContext db) : IRequestHandler<GetStocksQuery, List<StockDto>>
{
    public async Task<List<StockDto>> Handle(GetStocksQuery request, CancellationToken cancellationToken)
    {
        var query = db.Stocks
            .Include(s => s.Product)
                .ThenInclude(p => p.Unit)
            .Where(s => s.WarehouseId == request.WarehouseId);

        if (request.Search is not null)
            query = query.Where(s => s.Product.Name.Contains(request.Search));

        return await query
            .Select(s => new StockDto(
                s.Id,
                s.Product.Name,
                s.Product.Unit.Name,
                s.Quantity,
                s.PurchasePrice,
                s.SellingPrice,
                s.ExpiredAt))
            .ToListAsync(cancellationToken);
    }
}
