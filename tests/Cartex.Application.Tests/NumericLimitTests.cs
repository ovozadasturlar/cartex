using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Common.Exceptions;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

/// Written from docs/domain-rules.md SOZ-02 and SOZ-02a. A numeric limit is optional:
/// empty means there is no limit, zero means the operation is closed outright. Reading
/// zero as "no limit" turns the owner's strictest setting into unlimited permission.
[Collection("database")]
public class NumericLimitTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private sealed record Setup(long Warehouse, long VariantId, long CustomerId);

    private async Task<Setup> SetupAsync(decimal? creditLimit, decimal? maxDiscountPercent = null)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branch = (await db.Branches.FirstAsync(b => b.Name == "Asosiy filial")).Id;
        var warehouse = (await db.Warehouses.FirstAsync(w => w.Name == "Asosiy ombor")).Id;
        var business = (await db.Businesses.FirstAsync()).Id;
        var admin = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;

        Fixture.CurrentUser.AsAdmin(admin, business, branch);
        await TestShift.OpenAsync(Fixture);

        await scope.ServiceProvider.GetRequiredService<ISettingsService>()
            .SetAsync(SettingKeys.SalesPolicy, new SalesPolicySettings
            {
                AllowDebtSales = true,
                MaxDiscountPercent = maxDiscountPercent
            });

        var stocked = db.Stocks.Where(s => s.WarehouseId == warehouse && s.Quantity >= 3).Select(s => s.VariantId);
        var variantId = await db.ProductPrices
            .Where(p => p.WarehouseId == null && stocked.Contains(p.VariantId))
            .Select(p => p.VariantId)
            .OrderBy(x => x)
            .FirstAsync();

        var customer = await db.Customers.OrderBy(x => x.Id).FirstAsync();
        customer.CreditLimit = creditLimit;
        await db.SaveChangesAsync();

        return new Setup(warehouse, variantId, customer.Id);
    }

    private static CreateSaleCommand DebtSale(Setup s) =>
        new(s.Warehouse, s.CustomerId, 0, 0, 0, [new CreateSaleItemDto(s.VariantId, 1)])
        {
            ApplyAutoDiscount = false
        };

    [Fact]
    public async Task SOZ_02a_A_zero_credit_limit_closes_debt_for_that_customer()
    {
        var s = await SetupAsync(creditLimit: 0);

        using var scope = Fixture.CreateScope();
        var error = await Assert.ThrowsAnyAsync<Exception>(
            () => scope.ServiceProvider.GetRequiredService<ISender>().Send(DebtSale(s)));

        Assert.True(error is BusinessRuleException,
            $"expected the sale to be refused, got {error.GetType().Name}: {error.Message}");
    }

    [Fact]
    public async Task SOZ_02a_An_empty_credit_limit_leaves_debt_unlimited()
    {
        var s = await SetupAsync(creditLimit: null);

        using var scope = Fixture.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(DebtSale(s));

        Assert.True(result.SaleId > 0);
    }

    /// The limit is deliberately bypassable by `sales.discountOverride`, so the actor here
    /// is a cashier who only holds the everyday selling permissions.
    [Fact]
    public async Task SOZ_02_A_zero_discount_limit_refuses_a_cashier_any_discount()
    {
        var s = await SetupAsync(creditLimit: null, maxDiscountPercent: 0);

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branch = (await db.Branches.FirstAsync(b => b.Name == "Asosiy filial")).Id;
        var business = (await db.Businesses.FirstAsync()).Id;
        var admin = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;
        Fixture.CurrentUser.AsCashier(admin, business, branch);
        Fixture.CurrentUser.Granted.UnionWith(
            ["sales.create", "sales.checkout", "sales.discount", "sales.view", "shifts.open", "customers.view", "rates.view"]);

        var sale = new CreateSaleCommand(s.Warehouse, null, 1_000_000m, 0, 0,
            [new CreateSaleItemDto(s.VariantId, 1)]) { ApplyAutoDiscount = false, DiscountAmount = 100 };

        var error = await Assert.ThrowsAnyAsync<Exception>(
            () => scope.ServiceProvider.GetRequiredService<ISender>().Send(sale));

        Assert.True(error is BusinessRuleException,
            $"expected the discount to be refused, got {error.GetType().Name}: {error.Message}");
    }
}
