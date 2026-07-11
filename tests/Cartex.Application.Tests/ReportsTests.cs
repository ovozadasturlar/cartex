using Cartex.Application.Customers.Commands;
using Cartex.Application.Reports.Queries;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class ReportsTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private async Task<(long branch1, long warehouse1, long businessId, long adminId, long variantId, decimal price)> SetupAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branch1 = (await db.Branches.FirstAsync(b => b.Name == "Filial 1")).Id;
        var warehouse1 = (await db.Warehouses.FirstAsync(w => w.Name == "Filial 1 ombori")).Id;
        var businessId = (await db.Businesses.FirstAsync()).Id;
        var adminId = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;
        var productId = (await db.Products.FirstAsync(p => p.Name == "Coca-Cola 1.5L")).Id;
        var variantId = (await db.ProductVariants.FirstAsync(v => v.ProductId == productId)).Id;
        var price = await db.ProductPrices.Where(p => p.VariantId == variantId).Select(p => p.SellingPrice).FirstAsync();
        return (branch1, warehouse1, businessId, adminId, variantId, price);
    }

    private async Task<decimal> ReportRevenueAsync()
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var report = await sender.Send(new GetSalesReportQuery(
            DateTime.UtcNow.Date.AddDays(-1), DateTime.UtcNow.Date.AddDays(2), null));
        return report.Revenue;
    }

    [Fact]
    public async Task Sales_report_revenue_drops_after_partial_return()
    {
        var (branch1, warehouse1, businessId, adminId, variantId, price) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await TestShift.OpenAsync(Fixture);

        var baseline = await ReportRevenueAsync();

        long saleId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            saleId = (await sender.Send(new CreateSaleCommand(warehouse1, null, price * 2, 0, 0, [new CreateSaleItemDto(variantId, 2)]))).SaleId;
        }

        Assert.Equal(baseline + price * 2, await ReportRevenueAsync());

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var item = await db.SaleItems.FirstAsync(i => i.SaleId == saleId);
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new ReturnSaleCommand(saleId, [new ReturnLineDto(item.Id, 1, true, null)]));
        }

        Assert.Equal(baseline + price, await ReportRevenueAsync());
    }

    [Fact]
    public async Task Debt_aging_puts_fresh_debt_in_first_bucket()
    {
        var (branch1, warehouse1, businessId, adminId, variantId, price) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await TestShift.OpenAsync(Fixture);

        long customerId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            customerId = await sender.Send(new CreateCustomerCommand("Qarzdor", "+998" + Random.Shared.NextInt64(100_000_000, 999_999_999), null, 0m, CreditLimit: 10_000_000m));
            await sender.Send(new CreateSaleCommand(warehouse1, customerId, 0, 0, 0, [new CreateSaleItemDto(variantId, 2)]));
        }

        using var scope2 = Fixture.CreateScope();
        var senderQ = scope2.ServiceProvider.GetRequiredService<ISender>();
        var report = await senderQ.Send(new GetDebtAgingReportQuery());

        var row = report.Rows.Single(r => r.CustomerId == customerId);
        Assert.Equal(price * 2, row.Balance);
        Assert.Equal("0-30", row.Bucket);
        Assert.True(row.DaysOverdue <= 30);
        Assert.True(report.Bucket0_30 >= price * 2);
    }
}
