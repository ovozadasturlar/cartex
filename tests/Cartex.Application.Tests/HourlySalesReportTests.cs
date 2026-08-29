using Cartex.Application.Common.Messaging;
using Cartex.Application.Products.Commands;
using Cartex.Application.Reports.Queries;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Supplies.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Persistence;
using Cartex.Shared.Models.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class HourlySalesReportTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private const int TashkentOffsetMinutes = 300;
    private static readonly DateTime LocalDay = new(2026, 3, 11);
    private static readonly DateTime TenLocalUtc = new(2026, 3, 11, 5, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ThirteenLocalUtc = new(2026, 3, 11, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime NextDayTenLocalUtc = new(2026, 3, 12, 5, 0, 0, DateTimeKind.Utc);

    private async Task<long> SetupAsync()
    {
        long branchId, warehouseId, businessId, adminId;
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            branchId = (await db.Branches.FirstAsync(x => x.Name == "Asosiy filial")).Id;
            warehouseId = (await db.Warehouses.FirstAsync(x => x.Name == "Asosiy ombor")).Id;
            businessId = (await db.Businesses.FirstAsync()).Id;
            adminId = (await db.Users.FirstAsync(x => x.Username == "admin")).Id;
        }

        Fixture.CurrentUser.AsAdmin(adminId, businessId, branchId);
        await TestShift.OpenAsync(Fixture);
        return warehouseId;
    }

    private async Task<long> CreateProductAsync(string name, decimal sellingPrice, decimal purchasePrice, decimal quantity, long warehouseId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var unitId = await db.Units.Where(x => x.IsDefault).Select(x => x.Id).FirstAsync();
        var productId = await sender.Send(new CreateProductCommand(
            Name: name, CategoryId: null, UnitId: unitId, MinStock: null, Barcodes: null, SellingPrice: sellingPrice));
        var variantId = await db.ProductVariants.Where(x => x.ProductId == productId).Select(x => x.Id).SingleAsync();
        await sender.Send(new CreateSupplyCommand(null, warehouseId, DateOnly.FromDateTime(DateTime.Today),
            [new CreateSupplyItemDto(variantId, quantity, purchasePrice, null)]));
        return variantId;
    }

    private async Task<long> SellAsync(long warehouseId, decimal cash, decimal discount, params (long VariantId, decimal Qty)[] items)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var result = await sender.Send(new CreateSaleCommand(warehouseId, null, cash, 0, 0,
            [.. items.Select(x => new CreateSaleItemDto(x.VariantId, x.Qty))])
        {
            DiscountAmount = discount
        });
        return result.SaleId;
    }

    private async Task ReturnAsync(long saleId, long variantId, decimal quantity)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var itemId = await db.SaleItems.Where(x => x.SaleId == saleId && x.VariantId == variantId)
            .Select(x => x.Id).SingleAsync();
        await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(await TestReturns.ForItemAsync(db, itemId, quantity));
    }

    private async Task MoveToInstantAsync(long saleId, DateTime instantUtc)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sale = await db.Sales.FirstAsync(x => x.Id == saleId);
        sale.CreatedAt = instantUtc;
        await db.SaveChangesAsync();
    }

    private async Task<SalesReportDto> ReportAsync(int days = 1, int tzOffsetMinutes = TashkentOffsetMinutes)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(new GetSalesReportQuery(
            LocalDay.AddMinutes(-tzOffsetMinutes),
            LocalDay.AddDays(days).AddMinutes(-tzOffsetMinutes),
            null,
            tzOffsetMinutes));
    }

    private static HourlySalesDto HourOf(SalesReportDto report, int hour) =>
        Assert.Single(report.Hourly, x => x.Hour == hour);

    [Fact]
    public async Task HIS_07_hourly_rows_land_on_the_local_hour_of_each_sale()
    {
        var warehouseId = await SetupAsync();
        var variantId = await CreateProductAsync("Soatlik A", 1500m, 1000m, 100m, warehouseId);

        await MoveToInstantAsync(await SellAsync(warehouseId, 6000m, 0m, (variantId, 4m)), TenLocalUtc);
        await MoveToInstantAsync(await SellAsync(warehouseId, 3000m, 0m, (variantId, 2m)), ThirteenLocalUtc);

        var report = await ReportAsync();

        var morning = HourOf(report, 10);
        Assert.Equal(6000m, morning.Revenue);
        Assert.Equal(2000m, morning.Profit);
        Assert.Equal(1, morning.Count);

        var afternoon = HourOf(report, 13);
        Assert.Equal(3000m, afternoon.Revenue);
        Assert.Equal(1000m, afternoon.Profit);
        Assert.Equal(1, afternoon.Count);

        Assert.All(report.Hourly, x => Assert.InRange(x.Hour, 0, 23));
        Assert.DoesNotContain(report.Hourly, x => x.Count > 0 && x.Hour != 10 && x.Hour != 13);
    }

    [Fact]
    public async Task HIS_07_quiet_hours_between_sales_stay_in_the_row_with_zero()
    {
        var warehouseId = await SetupAsync();
        var variantId = await CreateProductAsync("Soatlik tinch", 1500m, 1000m, 100m, warehouseId);

        await MoveToInstantAsync(await SellAsync(warehouseId, 6000m, 0m, (variantId, 4m)), TenLocalUtc);
        await MoveToInstantAsync(await SellAsync(warehouseId, 3000m, 0m, (variantId, 2m)), ThirteenLocalUtc);

        var report = await ReportAsync();
        var band = report.Hourly.Where(x => x.Hour is >= 10 and <= 13).ToList();

        Assert.Equal(new[] { 10, 11, 12, 13 }, band.Select(x => x.Hour).ToArray());

        foreach (var quiet in band.Where(x => x.Hour is 11 or 12))
        {
            Assert.Equal(0m, quiet.Revenue);
            Assert.Equal(0m, quiet.Profit);
            Assert.Equal(0, quiet.Count);
        }
    }

    [Fact]
    public async Task HIS_07_hourly_totals_match_the_daily_row_and_the_report_total()
    {
        var warehouseId = await SetupAsync();
        var variantA = await CreateProductAsync("Soatlik yig'indi A", 1500m, 1000m, 100m, warehouseId);
        var variantB = await CreateProductAsync("Soatlik yig'indi B", 3000m, 2000m, 100m, warehouseId);

        await MoveToInstantAsync(
            await SellAsync(warehouseId, 10800m, 1200m, (variantA, 2m), (variantB, 3m)), TenLocalUtc);

        var afternoonId = await SellAsync(warehouseId, 6000m, 0m, (variantA, 4m));
        await ReturnAsync(afternoonId, variantA, 1m);
        await MoveToInstantAsync(afternoonId, ThirteenLocalUtc);

        var report = await ReportAsync();
        var day = Assert.Single(report.Daily);

        Assert.NotEqual(0m, report.Revenue);
        Assert.Equal(day.Revenue, report.Hourly.Sum(x => x.Revenue));
        Assert.Equal(day.Profit, report.Hourly.Sum(x => x.Profit));
        Assert.Equal(day.Count, report.Hourly.Sum(x => x.Count));
        Assert.Equal(report.Revenue, report.Hourly.Sum(x => x.Revenue));
        Assert.Equal(report.Profit, report.Hourly.Sum(x => x.Profit));
        Assert.Equal(report.SalesCount, report.Hourly.Sum(x => x.Count));
    }

    [Fact]
    public async Task HIS_01_hourly_revenue_drops_the_returned_portion_with_its_discount_share()
    {
        var warehouseId = await SetupAsync();
        var variantA = await CreateProductAsync("Soatlik qaytish A", 1500m, 1000m, 100m, warehouseId);
        var variantB = await CreateProductAsync("Soatlik qaytish B", 3000m, 2000m, 100m, warehouseId);

        var saleId = await SellAsync(warehouseId, 10800m, 1200m, (variantA, 2m), (variantB, 3m));
        await ReturnAsync(saleId, variantB, 1m);
        await MoveToInstantAsync(saleId, TenLocalUtc);

        var report = await ReportAsync();
        var hour = HourOf(report, 10);

        Assert.Equal(8100m, hour.Revenue);
        Assert.Equal(2100m, hour.Profit);
        Assert.Equal(1, hour.Count);

        var day = Assert.Single(report.Daily);
        Assert.Equal(day.Revenue, hour.Revenue);
        Assert.Equal(report.Revenue, hour.Revenue);
    }

    [Fact]
    public async Task HIS_07_multi_day_range_leaves_the_hourly_row_empty()
    {
        var warehouseId = await SetupAsync();
        var variantId = await CreateProductAsync("Soatlik ko'p kun", 1500m, 1000m, 100m, warehouseId);

        await MoveToInstantAsync(await SellAsync(warehouseId, 6000m, 0m, (variantId, 4m)), TenLocalUtc);
        await MoveToInstantAsync(await SellAsync(warehouseId, 3000m, 0m, (variantId, 2m)), NextDayTenLocalUtc);

        var oneDay = await ReportAsync();
        Assert.Single(oneDay.Daily);
        Assert.NotEmpty(oneDay.Hourly);

        var twoDays = await ReportAsync(days: 2);
        Assert.Equal(2, twoDays.Daily.Count);
        Assert.Empty(twoDays.Hourly);
    }

    [Fact]
    public async Task HIS_05_hourly_hour_follows_the_tz_offset()
    {
        var warehouseId = await SetupAsync();
        var variantId = await CreateProductAsync("Soatlik mintaqa", 1500m, 1000m, 100m, warehouseId);

        await MoveToInstantAsync(await SellAsync(warehouseId, 6000m, 0m, (variantId, 4m)), TenLocalUtc);

        var tashkent = await ReportAsync();
        var tashkentHour = Assert.Single(tashkent.Hourly, x => x.Count > 0);
        Assert.Equal(10, tashkentHour.Hour);
        Assert.Equal(6000m, tashkentHour.Revenue);

        var utc = await ReportAsync(tzOffsetMinutes: 0);
        var utcHour = Assert.Single(utc.Hourly, x => x.Count > 0);
        Assert.Equal(5, utcHour.Hour);
        Assert.Equal(6000m, utcHour.Revenue);
    }
}
