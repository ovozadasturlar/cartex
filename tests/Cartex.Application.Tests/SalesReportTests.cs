using Cartex.Application.Products.Commands;
using Cartex.Application.Reports.Queries;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Supplies.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Application.Warehouses.Commands;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class SalesReportTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private async Task<(long branch1, long warehouse1, long businessId, long adminId)> SetupAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branch1 = (await db.Branches.FirstAsync(b => b.Name == "Asosiy filial")).Id;
        var warehouse1 = (await db.Warehouses.FirstAsync(w => w.Name == "Asosiy ombor")).Id;
        var businessId = (await db.Businesses.FirstAsync()).Id;
        var adminId = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;
        return (branch1, warehouse1, businessId, adminId);
    }

    private async Task<long> CreateProductAsync(string name, decimal sellingPrice, decimal purchasePrice, decimal quantity, long warehouseId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var unitId = await db.Units.Where(u => u.IsDefault).Select(u => u.Id).FirstAsync();
        var productId = await sender.Send(new CreateProductCommand(
            Name: name, CategoryId: null, UnitId: unitId, MinStock: null, Barcodes: null, SellingPrice: sellingPrice));
        var variantId = await db.ProductVariants.Where(v => v.ProductId == productId).Select(v => v.Id).SingleAsync();
        await sender.Send(new CreateSupplyCommand(null, warehouseId, DateOnly.FromDateTime(DateTime.Today),
            [new CreateSupplyItemDto(variantId, quantity, purchasePrice, null)]));
        return variantId;
    }

    private async Task<long> SellAsync(long warehouseId, decimal cash, decimal discount, params (long VariantId, decimal Qty)[] items)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var result = await sender.Send(new CreateSaleCommand(warehouseId, null, cash, 0, 0,
            [.. items.Select(i => new CreateSaleItemDto(i.VariantId, i.Qty))], discount));
        return result.SaleId;
    }

    private async Task<SalesReportDto> ReportAsync(long? warehouseId = null)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(new GetSalesReportQuery(
            DateTime.UtcNow.Date.AddDays(-1), DateTime.UtcNow.Date.AddDays(2), warehouseId));
    }

    [Fact]
    public async Task Report_ComputesRevenueProfitAndTopProducts()
    {
        var (branch1, warehouse1, businessId, adminId) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await TestShift.OpenAsync(Fixture);

        var variantA = await CreateProductAsync("Hisobot A", 1500m, 1000m, 100m, warehouse1);
        var variantB = await CreateProductAsync("Hisobot B", 3000m, 2000m, 100m, warehouse1);

        await SellAsync(warehouse1, 6000m, 0m, (variantA, 4m));
        await SellAsync(warehouse1, 10800m, 1200m, (variantA, 2m), (variantB, 3m));

        var report = await ReportAsync();

        Assert.Equal(16800m, report.Revenue);
        Assert.Equal(4800m, report.Profit);
        Assert.Equal(2, report.SalesCount);
        Assert.Equal(8400m, report.AverageSale);
        Assert.Equal(10800m, report.MaxSale);

        Assert.Equal(2, report.TopProducts.Count);
        Assert.Equal("Hisobot A", report.TopProducts[0].ProductName);
        Assert.Equal(6m, report.TopProducts[0].Quantity);
        Assert.Equal(8700m, report.TopProducts[0].Revenue);
        Assert.Equal(2700m, report.TopProducts[0].Profit);
        Assert.Equal("Hisobot B", report.TopProducts[1].ProductName);
        Assert.Equal(3m, report.TopProducts[1].Quantity);
        Assert.Equal(8100m, report.TopProducts[1].Revenue);
        Assert.Equal(2100m, report.TopProducts[1].Profit);

        var day = Assert.Single(report.Daily);
        Assert.Equal(16800m, day.Revenue);
        Assert.Equal(4800m, day.Profit);
        Assert.Equal(2, day.Count);
    }

    [Fact]
    public async Task Report_ExcludesOtherWarehouse_WhenFiltered()
    {
        var (branch1, warehouse1, businessId, adminId) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await TestShift.OpenAsync(Fixture);

        long warehouse2;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            warehouse2 = await sender.Send(new CreateWarehouseCommand(branch1, "Hisobot ombori 2"));
        }

        var variantA = await CreateProductAsync("Filtr A", 1500m, 1000m, 10m, warehouse1);
        var variantB = await CreateProductAsync("Filtr B", 3000m, 2000m, 10m, warehouse2);

        await SellAsync(warehouse1, 3000m, 0m, (variantA, 2m));
        await SellAsync(warehouse2, 3000m, 0m, (variantB, 1m));

        var filtered = await ReportAsync(warehouse1);

        Assert.Equal(3000m, filtered.Revenue);
        Assert.Equal(1000m, filtered.Profit);
        Assert.Equal(1, filtered.SalesCount);
        var top = Assert.Single(filtered.TopProducts);
        Assert.Equal("Filtr A", top.ProductName);
        Assert.Equal(2m, top.Quantity);

        var all = await ReportAsync();
        Assert.Equal(6000m, all.Revenue);
        Assert.Equal(2, all.SalesCount);
    }

    [Fact]
    public async Task Report_HandlesPartialReturn()
    {
        var (branch1, warehouse1, businessId, adminId) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await TestShift.OpenAsync(Fixture);

        var variantId = await CreateProductAsync("Qaytish mahsuloti", 1500m, 1000m, 10m, warehouse1);
        var saleId = await SellAsync(warehouse1, 6000m, 0m, (variantId, 4m));

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var itemId = await db.SaleItems.Where(i => i.SaleId == saleId).Select(i => i.Id).SingleAsync();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(await TestReturns.ForItemAsync(db, itemId, 1m));
        }

        var report = await ReportAsync();

        Assert.Equal(4500m, report.Revenue);
        Assert.Equal(1500m, report.Profit);
        Assert.Equal(1, report.SalesCount);
        var top = Assert.Single(report.TopProducts);
        Assert.Equal(3m, top.Quantity);
        Assert.Equal(4500m, top.Revenue);
        Assert.Equal(1500m, top.Profit);
    }
}
