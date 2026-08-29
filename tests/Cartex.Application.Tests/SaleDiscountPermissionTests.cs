using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common.Exceptions;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class SaleDiscountPermissionTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private const decimal PriceA = 100_000m;
    private const decimal PriceB = 15_000m;
    private const decimal Gross = PriceA + PriceB;
    private const decimal Manual = 3_450m;

    private sealed record Setup(long Branch, long Warehouse, long Business, long Admin, long VariantA, long VariantB);

    private async Task<Setup> SetupAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branch = (await db.Branches.FirstAsync(b => b.Name == "Asosiy filial")).Id;
        var warehouse = (await db.Warehouses.FirstAsync(w => w.Name == "Asosiy ombor")).Id;
        var business = await db.Businesses.FirstAsync();
        var admin = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;

        var stocked = db.Stocks.Where(s => s.WarehouseId == warehouse && s.Quantity >= 3).Select(s => s.VariantId);
        var variants = await db.ProductPrices
            .Where(p => p.WarehouseId == null && stocked.Contains(p.VariantId))
            .Select(p => p.VariantId)
            .Distinct()
            .OrderBy(x => x)
            .Take(2)
            .ToListAsync();
        Assert.Equal(2, variants.Count);

        foreach (var price in await db.ProductPrices.Where(p => variants.Contains(p.VariantId)).ToListAsync())
        {
            price.SellingPrice = price.VariantId == variants[0] ? PriceA : PriceB;
            price.Currency = business.Currency;
        }
        await db.SaveChangesAsync();

        return new Setup(branch, warehouse, business.Id, admin, variants[0], variants[1]);
    }

    /// Puts the percentage ceiling out of the way so that only `sales.discount` can refuse anything here.
    private async Task ClearDiscountCeilingAsync()
    {
        using var scope = Fixture.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        await settings.SetAsync(SettingKeys.SalesPolicy, new SalesPolicySettings { MaxDiscountPercent = 100 });
    }

    private async Task AsCashierAsync(Setup s, params string[] permissions)
    {
        Fixture.CurrentUser.AsAdmin(s.Admin, s.Business, s.Branch);
        await TestShift.OpenAsync(Fixture);
        Fixture.CurrentUser.AsCashier(s.Admin, s.Business, s.Branch);
        foreach (var permission in permissions)
            Fixture.CurrentUser.Granted.Add(permission);
    }

    private static CreateSaleCommand Command(Setup s, decimal paid, decimal discount) =>
        new(s.Warehouse, null, paid, 0, 0,
            [new CreateSaleItemDto(s.VariantA, 1), new CreateSaleItemDto(s.VariantB, 1)])
        {
            DiscountAmount = discount,
            ApplyAutoDiscount = false
        };

    [Fact]
    public async Task CHEG_13_Manual_discount_requires_the_discount_permission()
    {
        var s = await SetupAsync();
        await ClearDiscountCeilingAsync();
        await AsCashierAsync(s, AppPermissions.Sales.Create, AppPermissions.Sales.Checkout);

        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var error = await Assert.ThrowsAnyAsync<Exception>(
            () => sender.Send(Command(s, Gross - Manual, Manual)));

        Assert.IsType<ForbiddenException>(error);
    }

    [Fact]
    public async Task CHEG_13_A_sale_without_a_discount_needs_no_extra_permission()
    {
        var s = await SetupAsync();
        await ClearDiscountCeilingAsync();
        await AsCashierAsync(s, AppPermissions.Sales.Create, AppPermissions.Sales.Checkout);

        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var saleId = (await sender.Send(Command(s, Gross, 0m))).SaleId;

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sale = await db.Sales.SingleAsync(x => x.Id == saleId);
        Assert.Equal(0m, sale.DiscountAmount);
        Assert.Equal(Gross, sale.TotalAmount);
    }
}
