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

/// Written from docs/domain-rules.md SOZ-11. Who must be named on a sale is the shop's policy:
/// a walk-in till wants nobody, a wholesale counter wants everybody on file.
[Collection("database")]
public class SaleCustomerRequirementTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private sealed record Setup(long Warehouse, long VariantId, long CustomerId);

    private async Task<Setup> SetupAsync(string requirement)
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
    public async Task SOZ_11_Always_refuses_a_sale_with_nobody_on_it()
    {
        var s = await SetupAsync("Always");

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

    [Theory]
    [InlineData("OnDebt")]
    [InlineData("Optional")]
    public async Task SOZ_11_A_fully_paid_sale_needs_nobody_under_the_looser_settings(string requirement)
    {
        var s = await SetupAsync(requirement);

        using var scope = Fixture.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(Sale(s, null));

        Assert.True(result.SaleId > 0);
    }

    [Fact]
    public async Task SOZ_11_Optional_still_refuses_a_debt_sale_with_nobody_to_owe_it()
    {
        var s = await SetupAsync("Optional");

        using var scope = Fixture.CreateScope();
        var error = await Assert.ThrowsAnyAsync<Exception>(
            () => scope.ServiceProvider.GetRequiredService<ISender>().Send(Sale(s, null) with { PaidCash = 0 }));

        Assert.IsType<BusinessRuleException>(error);
    }
}
