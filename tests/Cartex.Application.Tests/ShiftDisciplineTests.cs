using Cartex.Application.Customers.Commands;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Shifts.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Common.Exceptions;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class ShiftDisciplineTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private async Task<(long branch1, long warehouse1, long businessId, long adminId, long variantId, decimal price)> SetupAsync()
    {
        using var scope = fixture.CreateScope();
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

    [Fact]
    public async Task Cash_sale_without_open_shift_throws()
    {
        var (branch1, warehouse1, businessId, adminId, variantId, price) = await SetupAsync();
        fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);

        using var scope = fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            sender.Send(new CreateSaleCommand(warehouse1, null, price, 0, 0, [new CreateSaleItemDto(variantId, 1)])));
    }

    [Fact]
    public async Task Card_only_sale_without_shift_succeeds()
    {
        var (branch1, warehouse1, businessId, adminId, variantId, price) = await SetupAsync();
        fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);

        using var scope = fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var result = await sender.Send(new CreateSaleCommand(warehouse1, null, 0, price, 0, [new CreateSaleItemDto(variantId, 1)]));
        Assert.True(result.SaleId > 0);
    }

    [Fact]
    public async Task Cash_repay_without_shift_throws_card_repay_succeeds()
    {
        var (branch1, warehouse1, businessId, adminId, variantId, price) = await SetupAsync();
        fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);

        long customerId;
        using (var scope = fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            customerId = await sender.Send(new CreateCustomerCommand("Intizom Mijoz", "+9989" + Guid.NewGuid().ToString("N")[..8], null, 0m, CreditLimit: 10_000_000m));
            await sender.Send(new CreateSaleCommand(warehouse1, customerId, 0, 0, 0, [new CreateSaleItemDto(variantId, 2)]));
        }

        using (var scope = fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                sender.Send(new RepayCustomerDebtCommand(customerId, 1000m, false)));
            await sender.Send(new RepayCustomerDebtCommand(customerId, 1000m, true));
        }

        using var check = fixture.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var debt = (await db.Accounts.FirstAsync(a => a.CustomerId == customerId && a.Type == Cartex.Domain.Enums.AccountType.Debt)).Balance;
        Assert.Equal(price * 2 - 1000m, debt);
    }

    [Fact]
    public async Task Cash_return_after_shift_closed_throws()
    {
        var (branch1, warehouse1, businessId, adminId, variantId, price) = await SetupAsync();
        fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        var shiftId = await TestShift.OpenAsync(fixture);

        long saleId;
        using (var scope = fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            saleId = (await sender.Send(new CreateSaleCommand(warehouse1, null, price * 2, 0, 0, [new CreateSaleItemDto(variantId, 2)]))).SaleId;
            await sender.Send(new CloseShiftCommand(shiftId, 0));
        }

        using var scope2 = fixture.CreateScope();
        var db = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var item = await db.SaleItems.FirstAsync(i => i.SaleId == saleId);
        var sender2 = scope2.ServiceProvider.GetRequiredService<ISender>();
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            sender2.Send(new ReturnSaleCommand(saleId, [new ReturnLineDto(item.Id, 1, true, null)])));
    }

    [Fact]
    public async Task Z_report_includes_cash_debt_repayment()
    {
        var (branch1, warehouse1, businessId, adminId, variantId, price) = await SetupAsync();
        fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        var shiftId = await TestShift.OpenAsync(fixture);

        long customerId;
        using (var scope = fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            customerId = await sender.Send(new CreateCustomerCommand("Z Mijoz", "+9989" + Guid.NewGuid().ToString("N")[..8], null, 0m, CreditLimit: 10_000_000m));
            await sender.Send(new CreateSaleCommand(warehouse1, customerId, 0, 0, 0, [new CreateSaleItemDto(variantId, 2)]));
            await sender.Send(new RepayCustomerDebtCommand(customerId, 5000m, false));
        }

        using var scope2 = fixture.CreateScope();
        var sender2 = scope2.ServiceProvider.GetRequiredService<ISender>();
        var report = await sender2.Send(new CloseShiftCommand(shiftId, 5000m));

        Assert.Equal(5000m, report.DebtPayIn);
        Assert.Equal(5000m, report.ExpectedCash);
        Assert.Equal(0m, report.Difference);
    }
}
