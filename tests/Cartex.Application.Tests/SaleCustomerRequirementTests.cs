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

/// Written from docs/domain-rules.md SOZ-11. Debt, bonus spending and credit are impossible
/// without a customer whatever the setting says — the money lands on that customer's account.
/// What the setting decides is the fully paid sale.
[Collection("database")]
public class SaleCustomerRequirementTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private sealed record Setup(long Warehouse, long VariantId, long CustomerId);

    private async Task<Setup> SetupAsync(string requirement, bool loyaltyEnabled = true)
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
            .SetAsync(SettingKeys.SalesPolicy, new SalesPolicySettings { CustomerRequirement = requirement });

        foreach (var program in await db.LoyaltyPrograms.ToListAsync())
            program.IsEnabled = loyaltyEnabled;
        await db.SaveChangesAsync();

        var stocked = db.Stocks.Where(s => s.WarehouseId == warehouse && s.Quantity >= 3).Select(s => s.VariantId);
        var variantId = await db.ProductPrices
            .Where(p => p.WarehouseId == null && stocked.Contains(p.VariantId))
            .Select(p => p.VariantId)
            .OrderBy(x => x)
            .FirstAsync();

        return new Setup(warehouse, variantId, (await db.Customers.OrderBy(x => x.Id).FirstAsync()).Id);
    }

    private static CreateSaleCommand Sale(Setup s, long? customerId) =>
        new(s.Warehouse, customerId, 1_000_000m, 0, 0, [new CreateSaleItemDto(s.VariantId, 1)])
        {
            ApplyAutoDiscount = false
        };

    [Fact]
    public async Task SOZ_11_Always_refuses_a_paid_sale_with_nobody_on_it()
    {
        var s = await SetupAsync("Always", loyaltyEnabled: false);

        using var scope = Fixture.CreateScope();
        var error = await Assert.ThrowsAnyAsync<Exception>(
            () => scope.ServiceProvider.GetRequiredService<ISender>().Send(Sale(s, null)));

        Assert.True(error is BusinessRuleException { Code: "customer_required" },
            $"expected customer_required, got {error.GetType().Name}: {error.Message}");
    }

    [Fact]
    public async Task SOZ_11_Always_accepts_the_same_sale_once_a_customer_is_named()
    {
        var s = await SetupAsync("Always");

        using var scope = Fixture.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(Sale(s, s.CustomerId));

        Assert.True(result.SaleId > 0);
    }

    [Fact]
    public async Task SOZ_11_OnDebt_lets_a_paid_walk_in_through_even_with_loyalty_running()
    {
        var s = await SetupAsync("OnDebt");

        using var scope = Fixture.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(Sale(s, null));

        Assert.True(result.SaleId > 0);
    }

    [Fact]
    public async Task SOZ_11_OnBonus_demands_a_customer_while_the_shop_gives_cashback()
    {
        var s = await SetupAsync("OnBonus", loyaltyEnabled: true);

        using var scope = Fixture.CreateScope();
        var error = await Assert.ThrowsAnyAsync<Exception>(
            () => scope.ServiceProvider.GetRequiredService<ISender>().Send(Sale(s, null)));

        Assert.True(error is BusinessRuleException { Code: "customer_required" },
            $"expected customer_required, got {error.GetType().Name}: {error.Message}");
    }

    [Fact]
    public async Task SOZ_11_OnBonus_lets_a_walk_in_through_when_no_cashback_is_on_offer()
    {
        var s = await SetupAsync("OnBonus", loyaltyEnabled: false);

        using var scope = Fixture.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(Sale(s, null));

        Assert.True(result.SaleId > 0);
    }

    [Fact]
    public async Task SOZ_11_A_debt_sale_needs_a_customer_under_every_setting()
    {
        var s = await SetupAsync("OnDebt", loyaltyEnabled: false);

        using var scope = Fixture.CreateScope();
        var error = await Assert.ThrowsAnyAsync<Exception>(
            () => scope.ServiceProvider.GetRequiredService<ISender>().Send(Sale(s, null) with { PaidCash = 0 }));

        Assert.IsType<BusinessRuleException>(error);
    }
}
