using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Stocks.Queries;

public record GetStocksQuery : FilteringRequest, IRequest<IReadOnlyCollection<StockDto>>;

public record StockDto(long Id, long VariantId, string ProductName, string? CategoryName, string UnitName, decimal Quantity, decimal PurchasePrice, decimal SellingPrice, DateOnly? ExpiredAt);

public sealed class GetStocksQueryHandler(
    IApplicationDbContext db,
    IPagingMetadataWriter writer) : IRequestHandler<GetStocksQuery, IReadOnlyCollection<StockDto>>
{
    public async Task<IReadOnlyCollection<StockDto>> Handle(GetStocksQuery request, CancellationToken cancellationToken)
    {
        return await db.Stocks
            .ToPagedListAsync(request,
                s => new StockDto(
                    s.Id,
                    s.VariantId,
                    s.Variant.Product.Name,
                    s.Variant.Product.Category != null ? s.Variant.Product.Category.Name : null,
                    s.Variant.Product.Unit.Name,
                    s.Quantity,
                    s.PurchasePrice,
                    (s.Variant.Prices.Where(pp => pp.WarehouseId == s.WarehouseId).Select(pp => (decimal?)pp.SellingPrice).FirstOrDefault()
                        ?? s.Variant.Prices.Where(pp => pp.WarehouseId == null).Select(pp => (decimal?)pp.SellingPrice).FirstOrDefault()) ?? 0,
                    s.ExpiredAt),
                writer, cancellationToken);
    }
}
