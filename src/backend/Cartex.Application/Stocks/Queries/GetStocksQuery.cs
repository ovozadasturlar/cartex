using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Application.Common.Catalog;
using Cartex.Persistence;
using Cartex.Shared.Models.Stocks;

namespace Cartex.Application.Stocks.Queries;

public record GetStocksQuery : FilteringRequest, IRequest<IReadOnlyCollection<StockDto>>;

public sealed class GetStocksQueryHandler(
    IApplicationDbContext db,
    IPagingMetadataWriter writer) : IRequestHandler<GetStocksQuery, IReadOnlyCollection<StockDto>>
{
    public async Task<IReadOnlyCollection<StockDto>> Handle(GetStocksQuery request, CancellationToken cancellationToken)
    {
        var rows = await db.Stocks
            .ToPagedListAsync(request,
                s => new
                {
                    s.Id,
                    s.VariantId,
                    ProductName = s.Variant.Product.Name,
                    s.Variant.Product.CategoryId,
                    UnitName = s.Variant.Product.Unit.Name,
                    s.Quantity,
                    s.PurchasePrice,
                    SellingPrice = (s.Variant.Prices.Where(pp => pp.WarehouseId == s.WarehouseId).Select(pp => (decimal?)pp.SellingPrice).FirstOrDefault()
                        ?? s.Variant.Prices.Where(pp => pp.WarehouseId == null).Select(pp => (decimal?)pp.SellingPrice).FirstOrDefault()) ?? 0,
                    s.ExpiredAt
                },
                writer, cancellationToken);
        var paths = await CategoryPathLookup.LoadAsync(db, cancellationToken);
        return rows.Select(x => new StockDto(
            x.Id,
            x.VariantId,
            x.ProductName,
            x.CategoryId is { } categoryId ? paths.GetValueOrDefault(categoryId) : null,
            x.UnitName,
            x.Quantity,
            x.PurchasePrice,
            x.SellingPrice,
            x.ExpiredAt)).ToList();
    }
}
