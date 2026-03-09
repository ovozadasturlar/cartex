using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Stocks.Queries;

public record GetStocksQuery : FilteringRequest, IRequest<IReadOnlyCollection<StockDto>>;

public record StockDto(long Id, string ProductName, string UnitName, decimal Quantity, decimal PurchasePrice, decimal SellingPrice, DateOnly? ExpiredAt);

public sealed class GetStocksQueryHandler(
    IApplicationDbContext db,
    IPagingMetadataWriter writer) : IRequestHandler<GetStocksQuery, IReadOnlyCollection<StockDto>>
{
    public async Task<IReadOnlyCollection<StockDto>> Handle(GetStocksQuery request, CancellationToken cancellationToken)
    {
        return await db.Stocks
            .Include(s => s.Product)
                .ThenInclude(p => p.Unit)
            .Select(s => new StockDto(
                s.Id,
                s.Product.Name,
                s.Product.Unit.Name,
                s.Quantity,
                s.PurchasePrice,
                s.SellingPrice,
                s.ExpiredAt))
            .ToPagedListAsync(request, writer, cancellationToken);
    }
}
