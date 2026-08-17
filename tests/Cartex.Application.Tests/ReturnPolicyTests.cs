using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Application.CustomerReturns.Commands;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

/// Written from docs/domain-rules.md QAYT-08, QAYT-09, QAYT-10 and SOZ-12: every return switch is
/// the shop's own decision, and every one of them is enforced on the server — hiding the button
/// on a client is convenience, not a rule.
[Collection("database")]
public class ReturnPolicyTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private sealed record Setup(long Warehouse, long VariantId);

    private async Task<Setup> SetupAsync(Action<SalesPolicySettings> configure)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branch = (await db.Branches.FirstAsync(b => b.Name == "Asosiy filial")).Id;
        var warehouse = (await db.Warehouses.FirstAsync(w => w.Name == "Asosiy ombor")).Id;
        var business = (await db.Businesses.FirstAsync()).Id;
        var admin = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;

        Fixture.CurrentUser.AsAdmin(admin, business, branch);
        await TestShift.OpenAsync(Fixture);

        var policy = new SalesPolicySettings();
        configure(policy);
        await scope.ServiceProvider.GetRequiredService<ISettingsService>()
            .SetAsync(SettingKeys.SalesPolicy, policy);

        var stocked = db.Stocks.Where(s => s.WarehouseId == warehouse && s.Quantity >= 3).Select(s => s.VariantId);
        var variantId = await db.ProductPrices
            .Where(p => p.WarehouseId == null && stocked.Contains(p.VariantId))
            .Select(p => p.VariantId)
            .OrderBy(x => x)
            .FirstAsync();

        return new Setup(warehouse, variantId);
    }

    private async Task<long> SellAsync(Setup s)
    {
        using var scope = Fixture.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new CreateSaleCommand(s.Warehouse, null, 1_000_000m, 0, 0,
                [new CreateSaleItemDto(s.VariantId, 1)]) { ApplyAutoDiscount = false })).SaleId;
    }

    /// A refund pays out of the drawer, so the drawer has to have something in it. Voiding a sale
    /// takes its own cash straight back out, which is why the funding sale is a separate one.
    private Task FundTheDrawerAsync(Setup s) => SellAsync(s);

    private async Task VoidAsync(long saleId)
    {
        using var scope = Fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new VoidSaleCommand(saleId, "Xato kiritildi"));
    }

    [Fact]
    public async Task QAYT_08_The_shop_can_open_returns_on_voided_sales()
    {
        var s = await SetupAsync(p => p.AllowReturnOnVoidedSale = true);
        await FundTheDrawerAsync(s);
        var saleId = await SellAsync(s);
        await VoidAsync(saleId);

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var item = await db.SaleItems.AsNoTracking().FirstAsync(x => x.SaleId == saleId);

        var result = await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(await TestReturns.ForItemAsync(db, item.Id, 1m));

        Assert.True(result.RefundAmount > 0);
    }

    /// A return with no sale behind it also has no customer account to settle into, so the cash
    /// leg is named explicitly — that is an existing rule, not what these two tests are about.
    private static CreateCustomerReturnCommand FreeLine(Setup s) =>
        new(s.Warehouse,
            [new CustomerReturnLineInput(s.VariantId, 1m, null, 10_000m, null,
                ReturnItemCondition.Sellable, InventoryDisposition.SellableRestock)],
            Settlements: [new CustomerReturnSettlementInput(ReturnSettlementMethod.Cash, "UZS", 10_000m)],
            AutoSettle: false);

    [Fact]
    public async Task QAYT_09_A_free_line_is_refused_when_the_shop_turned_them_off()
    {
        var s = await SetupAsync(p => p.AllowFreeReturnLines = false);

        using var scope = Fixture.CreateScope();
        var error = await Assert.ThrowsAnyAsync<Exception>(
            () => scope.ServiceProvider.GetRequiredService<ISender>().Send(FreeLine(s)));

        Assert.True(error is BusinessRuleException { Code: "free_return_lines_disabled" },
            $"expected free_return_lines_disabled, got {error.GetType().Name}: {error.Message}");
    }

    [Fact]
    public async Task QAYT_09_A_free_line_is_accepted_by_default()
    {
        var s = await SetupAsync(_ => { });
        await FundTheDrawerAsync(s);

        using var scope = Fixture.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(FreeLine(s));

        Assert.Equal(10_000m, result.RefundAmount);
    }

    [Fact]
    public async Task QAYT_10_A_reason_is_demanded_on_every_line_when_the_shop_asks_for_it()
    {
        var s = await SetupAsync(p => p.RequireReturnReason = true);
        var saleId = await SellAsync(s);

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var item = await db.SaleItems.AsNoTracking().FirstAsync(x => x.SaleId == saleId);
        var command = await TestReturns.ForItemAsync(db, item.Id, 1m);

        var error = await Assert.ThrowsAnyAsync<Exception>(() => sender.Send(command));

        Assert.True(error is BusinessRuleException { Code: "return_reason_required" },
            $"expected return_reason_required, got {error.GetType().Name}: {error.Message}");

        var withReason = command with
        {
            Lines = [.. command.Lines.Select(l => l with { Reason = "Sifatsiz" })]
        };

        Assert.True((await sender.Send(withReason)).RefundAmount > 0);
    }
}
