using Cartex.Domain.Enums;
using Cartex.Application.Common.Catalog;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Cartex.Shared.Models.Reports;

namespace Cartex.Application.Reports.Queries;

public record GetSalesBreakdownReportQuery(DateTime From, DateTime To, long? WarehouseId) : IRequest<SalesBreakdownReportDto>;

public sealed class GetSalesBreakdownReportQueryHandler(IApplicationDbContext db) : IRequestHandler<GetSalesBreakdownReportQuery, SalesBreakdownReportDto>
{
    public async Task<SalesBreakdownReportDto> Handle(GetSalesBreakdownReportQuery request, CancellationToken cancellationToken)
    {
        var from = DateTime.SpecifyKind(request.From, DateTimeKind.Utc);
        var to = DateTime.SpecifyKind(request.To, DateTimeKind.Utc);

        // HIS-02: qisman qaytarilgan savdo ham savdo — uni chiqarib tashlash daromad kartasida
        // qolgan pulni to'lov taqsimotidan yo'q qiladi. Filtr savdo hisoboti bilan bir xil.
        var salesQuery = db.Sales.Where(s =>
            (s.Status == SaleStatus.Completed || s.Status == SaleStatus.PartialReturn)
            && s.CreatedAt >= from && s.CreatedAt < to);
        if (request.WarehouseId is { } warehouseId)
            salesQuery = salesQuery.Where(s => s.WarehouseId == warehouseId);

        var sales = await salesQuery
            .Select(s => new { s.Id, s.UserId, UserName = s.User.FullName, s.TotalAmount, s.DiscountAmount, s.PaidCash, s.PaidCard, s.PaidBonus, s.PaidAdvance, s.DebtAmount, s.CreditAmount })
            .ToListAsync(cancellationToken);

        var byCashier = sales
            .GroupBy(s => new { s.UserId, s.UserName })
            .Select(g => new CashierSalesDto(g.Key.UserId, g.Key.UserName, g.Sum(x => x.TotalAmount), g.Count()))
            .OrderByDescending(c => c.Revenue)
            .ToList();

        var itemsQuery = db.SaleItems.Where(i =>
            (i.Sale.Status == SaleStatus.Completed || i.Sale.Status == SaleStatus.PartialReturn)
            && i.Sale.CreatedAt >= from && i.Sale.CreatedAt < to);
        if (request.WarehouseId is { } wid)
            itemsQuery = itemsQuery.Where(i => i.Sale.WarehouseId == wid);

        var categoryRows = await itemsQuery
            .GroupBy(i => i.Variant.Product.CategoryId)
            .Select(g => new { CategoryId = g.Key, Quantity = g.Sum(x => x.Quantity), Revenue = g.Sum(x => x.UnitPrice * x.Quantity) })
            .OrderByDescending(c => c.Revenue)
            .ToListAsync(cancellationToken);
        var categoryPaths = await CategoryPathLookup.LoadAsync(db, cancellationToken);
        var byCategory = categoryRows.Select(x => new CategorySalesDto(
            x.CategoryId is { } categoryId ? categoryPaths.GetValueOrDefault(categoryId) : null,
            x.Quantity,
            x.Revenue)).ToList();

        // Qaytarilgan qism savdo hisobotidagi daromaddan chiqarib tashlanadi, lekin pul allaqachon
        // kassaga tushgan. Shuning uchun u alohida ustun bo'lib turadi — shunda HIS-04 tenglashadi.
        var returnedRows = await itemsQuery
            .Where(i => i.ReturnedQuantity > 0)
            .GroupBy(i => i.SaleId)
            .Select(g => new { SaleId = g.Key, Gross = g.Sum(x => x.ReturnedQuantity * x.UnitPrice) })
            .ToListAsync(cancellationToken);
        var grossBySale = await itemsQuery
            .GroupBy(i => i.SaleId)
            .Select(g => new { SaleId = g.Key, Gross = g.Sum(x => x.Quantity * x.UnitPrice) })
            .ToDictionaryAsync(x => x.SaleId, x => x.Gross, cancellationToken);
        var discountBySale = sales.ToDictionary(s => s.Id, s => s.DiscountAmount);
        var returned = returnedRows.Sum(r => r.Gross * (1 - SalesReportMath.DiscountRate(
            grossBySale.GetValueOrDefault(r.SaleId), discountBySale.GetValueOrDefault(r.SaleId))));

        return new SalesBreakdownReportDto(
            sales.Sum(s => s.PaidCash),
            sales.Sum(s => s.PaidCard),
            sales.Sum(s => s.PaidBonus),
            sales.Sum(s => s.DebtAmount),
            byCashier,
            byCategory,
            sales.Sum(s => s.CreditAmount),
            sales.Sum(s => s.PaidAdvance),
            returned);
    }
}
