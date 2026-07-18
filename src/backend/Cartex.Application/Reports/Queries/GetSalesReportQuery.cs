using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Reports.Queries;

public record GetSalesReportQuery(DateTime From, DateTime To, long? WarehouseId, int? TzOffsetMinutes = null) : IRequest<SalesReportDto>;

public record TopProductReportDto(long ProductId, string ProductName, decimal Quantity, decimal Revenue, decimal Profit);

public record DailySalesDto(DateTime Date, decimal Revenue, decimal Profit, int Count);

public record SalesReportDto(
    decimal Revenue,
    decimal Profit,
    int SalesCount,
    decimal AverageSale,
    decimal MaxSale,
    List<TopProductReportDto> TopProducts,
    List<DailySalesDto> Daily);

public sealed class GetSalesReportQueryHandler(IApplicationDbContext db) : IRequestHandler<GetSalesReportQuery, SalesReportDto>
{
    public async Task<SalesReportDto> Handle(GetSalesReportQuery request, CancellationToken cancellationToken)
    {
        var from = DateTime.SpecifyKind(request.From, DateTimeKind.Utc);
        var to = DateTime.SpecifyKind(request.To, DateTimeKind.Utc);

        var salesQuery = db.Sales.Where(s =>
            (s.Status == SaleStatus.Completed || s.Status == SaleStatus.PartialReturn)
            && s.CreatedAt >= from && s.CreatedAt < to);
        if (request.WarehouseId is { } warehouseId)
            salesQuery = salesQuery.Where(s => s.WarehouseId == warehouseId);

        var sales = await salesQuery
            .Select(s => new { s.Id, s.CreatedAt, s.TotalAmount, s.DiscountAmount })
            .ToListAsync(cancellationToken);

        var itemsQuery = db.SaleItems.Where(i =>
            (i.Sale.Status == SaleStatus.Completed || i.Sale.Status == SaleStatus.PartialReturn)
            && i.Sale.CreatedAt >= from && i.Sale.CreatedAt < to);
        if (request.WarehouseId is { } wid)
            itemsQuery = itemsQuery.Where(i => i.Sale.WarehouseId == wid);
        var items = await itemsQuery
            .Select(i => new { i.SaleId, ProductId = i.Variant.ProductId, ProductName = i.Variant.Product.Name, i.Quantity, i.ReturnedQuantity, i.UnitPrice, i.PurchasePrice })
            .GroupBy(x => new { x.SaleId, x.ProductId, x.ProductName })
            .Select(g => new
            {
                g.Key.SaleId,
                g.Key.ProductId,
                g.Key.ProductName,
                Quantity = g.Sum(x => x.Quantity - x.ReturnedQuantity),
                Gross = g.Sum(x => x.Quantity * x.UnitPrice),
                NetGross = g.Sum(x => (x.Quantity - x.ReturnedQuantity) * x.UnitPrice),
                NetCost = g.Sum(x => (x.Quantity - x.ReturnedQuantity) * x.PurchasePrice)
            })
            .ToListAsync(cancellationToken);

        var grossBySale = items.GroupBy(i => i.SaleId).ToDictionary(g => g.Key, g => g.Sum(x => x.Gross));
        var rateBySale = sales.ToDictionary(s => s.Id, s => SalesReportMath.DiscountRate(grossBySale.GetValueOrDefault(s.Id), s.DiscountAmount));

        var lines = items.Select(i =>
        {
            var rate = rateBySale[i.SaleId];
            var revenue = i.NetGross * (1 - rate);
            return new
            {
                i.SaleId,
                i.ProductId,
                i.ProductName,
                i.Quantity,
                Revenue = revenue,
                Profit = revenue - i.NetCost
            };
        }).ToList();

        var revenue = lines.Sum(l => l.Revenue);
        var profit = lines.Sum(l => l.Profit);
        var count = sales.Count;

        var topProducts = lines
            .GroupBy(l => new { l.ProductId, l.ProductName })
            .Select(g => new TopProductReportDto(
                g.Key.ProductId,
                g.Key.ProductName,
                g.Sum(x => x.Quantity),
                g.Sum(x => x.Revenue),
                g.Sum(x => x.Profit)))
            .OrderByDescending(t => t.Revenue)
            .Take(10)
            .ToList();

        var offset = TimeSpan.FromMinutes(request.TzOffsetMinutes ?? (int)TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).TotalMinutes);
        var dateById = sales.ToDictionary(s => s.Id, s => DateTime.SpecifyKind((s.CreatedAt + offset).Date, DateTimeKind.Unspecified));
        var revenueByDate = lines.GroupBy(l => dateById[l.SaleId]).ToDictionary(g => g.Key, g => g.Sum(x => x.Revenue));
        var profitByDate = lines.GroupBy(l => dateById[l.SaleId]).ToDictionary(g => g.Key, g => g.Sum(x => x.Profit));

        var daily = sales
            .GroupBy(s => dateById[s.Id])
            .Select(g => new DailySalesDto(
                g.Key,
                revenueByDate.GetValueOrDefault(g.Key),
                profitByDate.GetValueOrDefault(g.Key),
                g.Count()))
            .OrderBy(d => d.Date)
            .ToList();

        return new SalesReportDto(
            revenue,
            profit,
            count,
            count > 0 ? revenue / count : 0,
            count > 0 ? sales.Max(s => s.TotalAmount) : 0,
            topProducts,
            daily);
    }
}
