using Cartex.Persistence;
using Cartex.Application.Common.Catalog;
using Microsoft.EntityFrameworkCore;
using Cartex.Shared.Models.Reports;

namespace Cartex.Application.Reports.Queries;

public record GetInventoryValuationReportQuery(long? WarehouseId) : IRequest<InventoryValuationReportDto>;

public sealed class GetInventoryValuationReportQueryHandler(IApplicationDbContext db) : IRequestHandler<GetInventoryValuationReportQuery, InventoryValuationReportDto>
{
    public async Task<InventoryValuationReportDto> Handle(GetInventoryValuationReportQuery request, CancellationToken cancellationToken)
    {
        var stocksQuery = db.Stocks.Where(s => s.Quantity > 0);
        if (request.WarehouseId is { } warehouseId)
            stocksQuery = stocksQuery.Where(s => s.WarehouseId == warehouseId);

        var rows = await stocksQuery
            .Select(s => new
            {
                WarehouseName = s.Warehouse.Name,
                s.Variant.Product.CategoryId,
                s.Quantity,
                Cost = s.Quantity * s.PurchasePrice,
                Retail = s.Quantity * ((s.Variant.Prices.Where(pp => pp.WarehouseId == s.WarehouseId).Select(pp => (decimal?)pp.SellingPrice).FirstOrDefault()
                    ?? s.Variant.Prices.Where(pp => pp.WarehouseId == null).Select(pp => (decimal?)pp.SellingPrice).FirstOrDefault()) ?? 0)
            })
            .ToListAsync(cancellationToken);

        var categoryPaths = await CategoryPathLookup.LoadAsync(db, cancellationToken);
        var byWarehouse = rows
            .GroupBy(r => r.WarehouseName)
            .Select(g => new InventoryValuationGroupDto(g.Key, g.Sum(x => x.Quantity), g.Sum(x => x.Cost), g.Sum(x => x.Retail)))
            .OrderByDescending(g => g.Cost)
            .ToList();

        var byCategory = rows
            .GroupBy(r => r.CategoryId is { } categoryId ? categoryPaths.GetValueOrDefault(categoryId) : null)
            .Select(g => new InventoryValuationGroupDto(g.Key, g.Sum(x => x.Quantity), g.Sum(x => x.Cost), g.Sum(x => x.Retail)))
            .OrderByDescending(g => g.Cost)
            .ToList();

        return new InventoryValuationReportDto(rows.Sum(r => r.Cost), rows.Sum(r => r.Retail), byWarehouse, byCategory);
    }
}
