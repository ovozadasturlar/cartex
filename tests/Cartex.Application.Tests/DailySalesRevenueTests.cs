using Cartex.Application.Common.Messaging;
using Cartex.Application.Products.Commands;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Sales.Queries;
using Cartex.Application.Supplies.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Authorization;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class DailySalesRevenueTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private async Task<(long BranchId, long WarehouseId, long BusinessId, long AdminId, long SellerId)> SetupAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (
            (await db.Branches.FirstAsync(x => x.Name == "Asosiy filial")).Id,
            (await db.Warehouses.FirstAsync(x => x.Name == "Asosiy ombor")).Id,
            (await db.Businesses.FirstAsync()).Id,
            (await db.Users.FirstAsync(x => x.Username == "admin")).Id,
            (await db.Users.FirstAsync(x => x.Username == "seller")).Id);
    }

    private async Task<long> CreateProductAsync(string name, decimal sellingPrice, decimal quantity, long warehouseId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var unitId = await db.Units.Where(x => x.IsDefault).Select(x => x.Id).FirstAsync();
        var productId = await sender.Send(new CreateProductCommand(
            Name: name, CategoryId: null, UnitId: unitId, MinStock: null, Barcodes: null, SellingPrice: sellingPrice));
        var variantId = await db.ProductVariants.Where(x => x.ProductId == productId).Select(x => x.Id).SingleAsync();
        await sender.Send(new CreateSupplyCommand(null, warehouseId, DateOnly.FromDateTime(DateTime.Today),
            [new CreateSupplyItemDto(variantId, quantity, sellingPrice / 2m, null)]));
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

    private async Task VoidAsync(long saleId)
    {
        using var scope = Fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new VoidSaleCommand(saleId, "Xato savdo"));
    }

    private async Task<(int Count, decimal Total)> DailyAsync()
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var points = await sender.Send(new GetDailySalesQuery
        {
            FromDate = DateTime.UtcNow.Date.AddDays(-1),
            ToDate = DateTime.UtcNow.Date.AddDays(2),
            TzOffsetMinutes = 300
        });
        return (points.Sum(x => x.Count), points.Sum(x => x.TotalAmount));
    }

    [Fact]
    public async Task HIS_06_daily_revenue_drops_the_returned_portion()
    {
        var setup = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(setup.AdminId, setup.BusinessId, setup.BranchId);
        await TestShift.OpenAsync(Fixture);

        var variantId = await CreateProductAsync("Kunlik qaytish", 1500m, 10m, setup.WarehouseId);
        var saleId = await SellAsync(setup.WarehouseId, 6000m, 0m, (variantId, 4m));

        Assert.Equal((1, 6000m), await DailyAsync());

        await ReturnAsync(saleId, variantId, 1m);

        Assert.Equal((1, 4500m), await DailyAsync());
    }

    [Fact]
    public async Task HIS_06_daily_revenue_applies_the_discount_share_to_the_returned_portion()
    {
        var setup = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(setup.AdminId, setup.BusinessId, setup.BranchId);
        await TestShift.OpenAsync(Fixture);

        var variantA = await CreateProductAsync("Kunlik A", 1500m, 10m, setup.WarehouseId);
        var variantB = await CreateProductAsync("Kunlik B", 3000m, 10m, setup.WarehouseId);

        var saleId = await SellAsync(setup.WarehouseId, 10800m, 1200m, (variantA, 2m), (variantB, 3m));

        Assert.Equal((1, 10800m), await DailyAsync());

        await ReturnAsync(saleId, variantB, 1m);

        Assert.Equal((1, 8100m), await DailyAsync());
    }

    [Fact]
    public async Task HIS_06_daily_revenue_leaves_out_voided_sales_entirely()
    {
        var setup = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(setup.AdminId, setup.BusinessId, setup.BranchId);
        await TestShift.OpenAsync(Fixture);

        var variantId = await CreateProductAsync("Kunlik bekor", 1500m, 10m, setup.WarehouseId);
        await SellAsync(setup.WarehouseId, 6000m, 0m, (variantId, 4m));
        var voidedId = await SellAsync(setup.WarehouseId, 3000m, 0m, (variantId, 2m));

        Assert.Equal((2, 9000m), await DailyAsync());

        await VoidAsync(voidedId);

        Assert.Equal((1, 6000m), await DailyAsync());
    }

    [Fact]
    public async Task RUXSAT_07_daily_revenue_without_viewAll_covers_only_own_sales()
    {
        var setup = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(setup.AdminId, setup.BusinessId, setup.BranchId);
        await TestShift.OpenAsync(Fixture);

        var variantId = await CreateProductAsync("Kunlik qamrov", 1500m, 20m, setup.WarehouseId);
        await SellAsync(setup.WarehouseId, 6000m, 0m, (variantId, 4m));

        Fixture.CurrentUser.AsAdmin(setup.SellerId, setup.BusinessId, setup.BranchId);
        await TestShift.OpenAsync(Fixture);
        await SellAsync(setup.WarehouseId, 3000m, 0m, (variantId, 2m));

        Fixture.CurrentUser.AsCashier(setup.SellerId, setup.BusinessId, setup.BranchId);
        Fixture.CurrentUser.Granted.Add(AppPermissions.Sales.View);

        Assert.Equal((1, 3000m), await DailyAsync());
    }

    [Fact]
    public async Task RUXSAT_07_daily_revenue_with_viewAll_covers_every_sale()
    {
        var setup = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(setup.AdminId, setup.BusinessId, setup.BranchId);
        await TestShift.OpenAsync(Fixture);

        var variantId = await CreateProductAsync("Kunlik qamrov", 1500m, 20m, setup.WarehouseId);
        await SellAsync(setup.WarehouseId, 6000m, 0m, (variantId, 4m));

        Fixture.CurrentUser.AsAdmin(setup.SellerId, setup.BusinessId, setup.BranchId);
        await TestShift.OpenAsync(Fixture);
        await SellAsync(setup.WarehouseId, 3000m, 0m, (variantId, 2m));

        Fixture.CurrentUser.AsCashier(setup.SellerId, setup.BusinessId, setup.BranchId);
        Fixture.CurrentUser.Granted.Add(AppPermissions.Sales.View);
        Fixture.CurrentUser.Granted.Add(AppPermissions.Sales.ViewAll);

        Assert.Equal((2, 9000m), await DailyAsync());
    }

    private async Task ReturnFullyAsync(long saleId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(await TestReturns.ForSaleAsync(db, saleId));
    }

    private async Task<SaleStatus> StatusAsync(long saleId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Sales.Where(x => x.Id == saleId).Select(x => x.Status).SingleAsync();
    }

    [Fact]
    public async Task HIS_01_daily_revenue_leaves_out_fully_returned_sales_entirely()
    {
        var setup = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(setup.AdminId, setup.BusinessId, setup.BranchId);
        await TestShift.OpenAsync(Fixture);

        var variantId = await CreateProductAsync("Kunlik to'liq qaytish", 1500m, 20m, setup.WarehouseId);
        await SellAsync(setup.WarehouseId, 6000m, 0m, (variantId, 4m));
        var returnedId = await SellAsync(setup.WarehouseId, 3000m, 0m, (variantId, 2m));

        Assert.Equal((2, 9000m), await DailyAsync());

        await ReturnFullyAsync(returnedId);
        Assert.Equal(SaleStatus.Returned, await StatusAsync(returnedId));

        Assert.Equal((1, 6000m), await DailyAsync());
    }
}
