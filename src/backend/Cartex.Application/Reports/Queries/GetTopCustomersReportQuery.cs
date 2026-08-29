using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Cartex.Shared.Models.Reports;

namespace Cartex.Application.Reports.Queries;

public record GetTopCustomersReportQuery(DateTime From, DateTime To, long? WarehouseId) : IRequest<List<CustomerSalesDto>>;

public sealed class GetTopCustomersReportQueryHandler(IApplicationDbContext db) : IRequestHandler<GetTopCustomersReportQuery, List<CustomerSalesDto>>
{
    public async Task<List<CustomerSalesDto>> Handle(GetTopCustomersReportQuery request, CancellationToken cancellationToken)
    {
        var from = DateTime.SpecifyKind(request.From, DateTimeKind.Utc);
        var to = DateTime.SpecifyKind(request.To, DateTimeKind.Utc);

        var salesQuery = db.Sales.Where(s =>
            (s.Status == SaleStatus.Completed || s.Status == SaleStatus.PartialReturn)
            && s.CustomerId != null && s.CreatedAt >= from && s.CreatedAt < to);
        if (request.WarehouseId is { } warehouseId)
            salesQuery = salesQuery.Where(s => s.WarehouseId == warehouseId);

        var sales = await salesQuery
            .Select(s => new { s.Id, CustomerId = s.CustomerId!.Value, CustomerName = s.Customer!.Party.FullName, s.CreatedAt, s.DiscountAmount })
            .ToListAsync(cancellationToken);

        var itemsQuery = db.SaleItems.Where(i =>
            (i.Sale.Status == SaleStatus.Completed || i.Sale.Status == SaleStatus.PartialReturn)
            && i.Sale.CustomerId != null && i.Sale.CreatedAt >= from && i.Sale.CreatedAt < to);
        if (request.WarehouseId is { } wid)
            itemsQuery = itemsQuery.Where(i => i.Sale.WarehouseId == wid);
        var items = await itemsQuery
            .Select(i => new { i.SaleId, i.Quantity, i.ReturnedQuantity, i.UnitPrice, i.PurchasePrice })
            .ToListAsync(cancellationToken);

        var grossBySale = items.GroupBy(i => i.SaleId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity * x.UnitPrice));
        var rateBySale = sales.ToDictionary(s => s.Id, s => SalesReportMath.DiscountRate(grossBySale.GetValueOrDefault(s.Id), s.DiscountAmount));

        var totalsBySale = items
            .GroupBy(i => i.SaleId)
            .ToDictionary(g => g.Key, g => (
                Revenue: g.Sum(x => SalesReportMath.NetRevenue(x.Quantity, x.ReturnedQuantity, x.UnitPrice, rateBySale[g.Key])),
                Profit: g.Sum(x => SalesReportMath.Profit(x.Quantity, x.ReturnedQuantity, x.UnitPrice, x.PurchasePrice, rateBySale[g.Key]))));

        return sales
            .GroupBy(s => new { s.CustomerId, s.CustomerName })
            .Select(g => new CustomerSalesDto(
                g.Key.CustomerId,
                g.Key.CustomerName,
                g.Sum(s => totalsBySale.GetValueOrDefault(s.Id).Revenue),
                g.Sum(s => totalsBySale.GetValueOrDefault(s.Id).Profit),
                g.Count(),
                g.Max(s => s.CreatedAt)))
            .OrderByDescending(c => c.Profit)
            .Take(20)
            .ToList();
    }
}
