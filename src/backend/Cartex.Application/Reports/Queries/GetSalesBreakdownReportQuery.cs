using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Reports.Queries;

public record GetSalesBreakdownReportQuery(DateTime From, DateTime To, long? WarehouseId) : IRequest<SalesBreakdownReportDto>;

public record CashierSalesDto(long UserId, string UserName, decimal Revenue, int Count);

public record CategorySalesDto(string? CategoryName, decimal Quantity, decimal Revenue);

public record SalesBreakdownReportDto(
    decimal Cash,
    decimal Card,
    decimal Bonus,
    decimal Debt,
    List<CashierSalesDto> ByCashier,
    List<CategorySalesDto> ByCategory,
    decimal Credit = 0,
    decimal Advance = 0);

public sealed class GetSalesBreakdownReportQueryHandler(IApplicationDbContext db) : IRequestHandler<GetSalesBreakdownReportQuery, SalesBreakdownReportDto>
{
    public async Task<SalesBreakdownReportDto> Handle(GetSalesBreakdownReportQuery request, CancellationToken cancellationToken)
    {
        var from = DateTime.SpecifyKind(request.From, DateTimeKind.Utc);
        var to = DateTime.SpecifyKind(request.To, DateTimeKind.Utc);

        var salesQuery = db.Sales.Where(s => s.Status == SaleStatus.Completed && s.CreatedAt >= from && s.CreatedAt < to);
        if (request.WarehouseId is { } warehouseId)
            salesQuery = salesQuery.Where(s => s.WarehouseId == warehouseId);

        var sales = await salesQuery
            .Select(s => new { s.Id, s.UserId, UserName = s.User.FullName, s.TotalAmount, s.PaidCash, s.PaidCard, s.PaidBonus, s.PaidAdvance, s.DebtAmount, s.CreditAmount })
            .ToListAsync(cancellationToken);

        var byCashier = sales
            .GroupBy(s => new { s.UserId, s.UserName })
            .Select(g => new CashierSalesDto(g.Key.UserId, g.Key.UserName, g.Sum(x => x.TotalAmount), g.Count()))
            .OrderByDescending(c => c.Revenue)
            .ToList();

        var itemsQuery = db.SaleItems.Where(i =>
            i.Sale.Status == SaleStatus.Completed && i.Sale.CreatedAt >= from && i.Sale.CreatedAt < to);
        if (request.WarehouseId is { } wid)
            itemsQuery = itemsQuery.Where(i => i.Sale.WarehouseId == wid);

        var categoryRows = await itemsQuery
            .GroupBy(i => i.Variant.Product.Category != null ? i.Variant.Product.Category.Name : null)
            .Select(g => new { Name = g.Key, Quantity = g.Sum(x => x.Quantity), Revenue = g.Sum(x => x.UnitPrice * x.Quantity) })
            .OrderByDescending(c => c.Revenue)
            .ToListAsync(cancellationToken);
        var byCategory = categoryRows.Select(x => new CategorySalesDto(x.Name, x.Quantity, x.Revenue)).ToList();

        return new SalesBreakdownReportDto(
            sales.Sum(s => s.PaidCash),
            sales.Sum(s => s.PaidCard),
            sales.Sum(s => s.PaidBonus),
            sales.Sum(s => s.DebtAmount),
            byCashier,
            byCategory,
            sales.Sum(s => s.CreditAmount),
            sales.Sum(s => s.PaidAdvance));
    }
}
