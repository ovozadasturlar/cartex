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
public class ReportDayBucketingTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private const int TzOffsetMinutes = 300;
    private static readonly DateTime SaleInstantUtc = new(2026, 3, 10, 22, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime LocalDay = new(2026, 3, 11);
    private static readonly DateTime FromUtc = LocalDay.AddMinutes(-TzOffsetMinutes);
    private static readonly DateTime ToUtc = LocalDay.AddDays(2).AddMinutes(-TzOffsetMinutes);

    private async Task<decimal> CreateSaleAtInstantAsync()
    {
        long branch1, warehouse1, businessId, adminId, variantId;
        decimal price;
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            branch1 = (await db.Branches.FirstAsync(b => b.Name == "Filial 1")).Id;
            warehouse1 = (await db.Warehouses.FirstAsync(w => w.Name == "Filial 1 ombori")).Id;
            businessId = (await db.Businesses.FirstAsync()).Id;
            adminId = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;
            var productId = (await db.Products.FirstAsync(p => p.Name == "Smesitel oshxona Zegor")).Id;
            variantId = (await db.ProductVariants.FirstAsync(v => v.ProductId == productId)).Id;
            price = await db.ProductPrices.Where(p => p.VariantId == variantId).Select(p => p.SellingPrice).FirstAsync();
        }

        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await TestShift.OpenAsync(Fixture);

        long saleId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            saleId = (await sender.Send(new CreateSaleCommand(warehouse1, null, price * 2, 0, 0, [new CreateSaleItemDto(variantId, 2)]))).SaleId;
        }

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var sale = await db.Sales.FirstAsync(s => s.Id == saleId);
            sale.CreatedAt = SaleInstantUtc;
            await db.SaveChangesAsync();
        }

        return price * 2;
    }

    [Fact]
    public async Task Sales_report_buckets_sale_on_local_day()
    {
        var total = await CreateSaleAtInstantAsync();

        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var report = await sender.Send(new GetSalesReportQuery(FromUtc, ToUtc, null, TzOffsetMinutes));

        Assert.Equal(1, report.SalesCount);
        var day = Assert.Single(report.Daily);
        Assert.Equal(LocalDay, day.Date);
        Assert.Equal(DateTimeKind.Unspecified, day.Date.Kind);
        Assert.Equal(total, day.Revenue);
        Assert.Equal(1, day.Count);
    }

    [Fact]
    public async Task Cash_flow_buckets_sale_on_local_day_and_keeps_last_local_day()
    {
        var total = await CreateSaleAtInstantAsync();

        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var flow = (await sender.Send(new GetCashFlowQuery(FromUtc, ToUtc, TzOffsetMinutes))).ToList();

        Assert.Equal(new[] { LocalDay, LocalDay.AddDays(1) }, flow.Select(d => d.Date).ToArray());
        Assert.Equal(DateTimeKind.Unspecified, flow[0].Date.Kind);
        Assert.Equal(total, flow[0].Sales);
        Assert.Equal(0m, flow[1].Sales);
    }
}
