using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Cartex.Shared.Models.Stocks;

namespace Cartex.Application.Stocks.Queries;

public record GetExpiringStocksQuery(int WithinDays = 30) : IRequest<IReadOnlyCollection<ExpiringStockDto>>;

public sealed class GetExpiringStocksQueryHandler(IApplicationDbContext db) : IRequestHandler<GetExpiringStocksQuery, IReadOnlyCollection<ExpiringStockDto>>
{
    public async Task<IReadOnlyCollection<ExpiringStockDto>> Handle(GetExpiringStocksQuery request, CancellationToken cancellationToken)
    {
        var threshold = DateOnly.FromDateTime(DateTime.Now).AddDays(request.WithinDays);

        return await db.Stocks
            .Where(s => s.Quantity > 0 && s.ExpiredAt != null && s.ExpiredAt <= threshold)
            .OrderBy(s => s.ExpiredAt)
            .Select(s => new ExpiringStockDto(s.Id, s.Variant.Product.Name, s.Warehouse.Name, s.Quantity, s.ExpiredAt!.Value))
            .ToListAsync(cancellationToken);
    }
}
