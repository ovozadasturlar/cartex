using Cartex.Application.Common.Messaging;
using Cartex.Application.Products.Commands;
using Cartex.Application.Reports.Queries;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Supplies.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Cartex.Shared.Models.Reports;

namespace Cartex.Application.Tests;

/// Written from docs/domain-rules.md §12 (HIS-01…HIS-05). The same person wrote the fix, so the
/// rule was written first and this test was shown failing before the queries were touched.
[Collection("database")]
public class ReportReconciliationTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private const decimal UnitPrice = 100_000m;

    private sealed record Setup(long Warehouse, long Business, long Admin, long Branch, long VariantId);

    private async Task<Setup> SetupAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var branch = (await db.Branches.FirstAsync(b => b.Name == "Asosiy filial")).Id;
        var warehouse = (await db.Warehouses.FirstAsync(w => w.Name == "Asosiy ombor")).Id;
        var business = (await db.Businesses.FirstAsync()).Id;
        var admin = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;

        Fixture.CurrentUser.AsAdmin(admin, business, branch);
        await TestShift.OpenAsync(Fixture);

        var unitId = await db.Units.Where(u => u.IsDefault).Select(u => u.Id).FirstAsync();
        var productId = await sender.Send(new CreateProductCommand(
            Name: "Hisobot sinovi", CategoryId: null, UnitId: unitId, MinStock: null,
            Barcodes: null, SellingPrice: UnitPrice));
        var variantId = await db.ProductVariants.Where(v => v.ProductId == productId).Select(v => v.Id).SingleAsync();
        await sender.Send(new CreateSupplyCommand(null, warehouse, DateOnly.FromDateTime(DateTime.Today),
            [new CreateSupplyItemDto(variantId, 20m, 40_000m, null)]));

        return new Setup(warehouse, business, admin, branch, variantId);
    }

    private async Task<long> SellAsync(Setup s, decimal quantity, decimal cash)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return (await sender.Send(new CreateSaleCommand(s.Warehouse, null, cash, 0, 0,
            [new CreateSaleItemDto(s.VariantId, quantity)])
        {
            ApplyAutoDiscount = false
        })).SaleId;
    }

    private async Task ReturnOneAsync(long saleId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var item = await db.SaleItems.AsNoTracking().FirstAsync(x => x.SaleId == saleId);
        await sender.Send(await TestReturns.ForItemAsync(db, item.Id, 1m));
    }

    private (DateTime From, DateTime To) Range() =>
        (DateTime.UtcNow.Date.AddDays(-1), DateTime.UtcNow.Date.AddDays(2));

    private async Task<SalesReportDto> RevenueAsync(Setup s)
    {
        using var scope = Fixture.CreateScope();
        var (from, to) = Range();
        return await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new GetSalesReportQuery(from, to, s.Warehouse));
    }

    private async Task<SalesBreakdownReportDto> BreakdownAsync(Setup s)
    {
        using var scope = Fixture.CreateScope();
        var (from, to) = Range();
        return await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new GetSalesBreakdownReportQuery(from, to, s.Warehouse));
    }

    /// HIS-02: a partially returned sale is still a sale; dropping it from the breakdown makes
    /// the money it brought in disappear from the report.
    [Fact]
    public async Task HIS_02_Partially_returned_sale_stays_in_the_payment_breakdown()
    {
        var s = await SetupAsync();
        await SellAsync(s, 2m, 200_000m);
        var returned = await SellAsync(s, 2m, 200_000m);
        await ReturnOneAsync(returned);

        var breakdown = await BreakdownAsync(s);

        // Two sales of 200 000 were paid in cash; one of them was later partly returned.
        Assert.Equal(400_000m, breakdown.Cash);
        Assert.Equal(2, breakdown.ByCashier.Sum(c => c.Count));
    }

    /// HIS-04: the columns the owner sees must add up to the revenue figure next to them.
    [Fact]
    public async Task HIS_04_Breakdown_columns_reconcile_with_the_revenue_figure()
    {
        var s = await SetupAsync();
        await SellAsync(s, 2m, 200_000m);
        var returned = await SellAsync(s, 2m, 200_000m);
        await ReturnOneAsync(returned);

        var revenue = (await RevenueAsync(s)).Revenue;
        var b = await BreakdownAsync(s);

        var reconciled = b.Cash + b.Card + b.Bonus + b.Advance + b.Debt - b.Credit - b.Returned;

        Assert.Equal(300_000m, revenue);         // 400 000 sotildi, 100 000 qaytdi
        Assert.Equal(100_000m, b.Returned);
        Assert.Equal(revenue, reconciled);
    }

    /// HIS-01/HIS-02: both cards must describe the same set of sales, so a clean period with no
    /// returns reconciles exactly with nothing subtracted.
    [Fact]
    public async Task HIS_01_Period_without_returns_reconciles_exactly()
    {
        var s = await SetupAsync();
        await SellAsync(s, 3m, 300_000m);

        var revenue = (await RevenueAsync(s)).Revenue;
        var b = await BreakdownAsync(s);

        Assert.Equal(0m, b.Returned);
        Assert.Equal(revenue, b.Cash + b.Card + b.Bonus + b.Advance + b.Debt - b.Credit - b.Returned);
    }
}
