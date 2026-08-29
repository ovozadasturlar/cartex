using Cartex.Application.Sales.Commands;
using Cartex.Application.Sales.Queries;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class ChangeAmountTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private async Task<(long branch1, long warehouse1, long businessId, long adminId, long variantId, decimal price)> SetupAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branch1 = (await db.Branches.FirstAsync(b => b.Name == "Asosiy filial")).Id;
        var warehouse1 = (await db.Warehouses.FirstAsync(w => w.Name == "Asosiy ombor")).Id;
        var businessId = (await db.Businesses.FirstAsync()).Id;
        var adminId = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;
        var productId = (await db.Products.FirstAsync(p => p.Name == "Smesitel oshxona Zegor")).Id;
        var variantId = (await db.ProductVariants.FirstAsync(v => v.ProductId == productId)).Id;
        var price = await db.ProductPrices.Where(p => p.VariantId == variantId).Select(p => p.SellingPrice).FirstAsync();
        return (branch1, warehouse1, businessId, adminId, variantId, price);
    }

    [Fact]
    public async Task Overpay_records_change_and_posts_net_cash()
    {
        var (branch1, warehouse1, businessId, adminId, variantId, price) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await TestShift.OpenAsync(Fixture);

        var total = price * 2;
        decimal cashBefore;
        string token;
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            cashBefore = await db.Accounts.Where(a => a.BranchId == branch1 && a.Type == AccountType.Cash)
                .Select(a => a.Balance).FirstOrDefaultAsync();

            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            token = (await sender.Send(new CreateSaleCommand(warehouse1, null, total + 5000m, 0, 0, [new CreateSaleItemDto(variantId, 2)]))).ReceiptToken;
        }

        using var check = Fixture.CreateScope();
        var db2 = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sale = await db2.Sales.FirstAsync(s => s.ReceiptToken == token);
        var cashAfter = (await db2.Accounts.FirstAsync(a => a.BranchId == branch1 && a.Type == AccountType.Cash)).Balance;

        Assert.Equal(5000m, sale.ChangeAmount);
        Assert.Equal(total, sale.PaidCash);
        Assert.Equal(cashBefore + total, cashAfter);
        Assert.Equal(sale.TotalAmount, sale.PaidCash + sale.PaidCard + sale.PaidBonus + sale.DebtAmount);

        var sender2 = check.ServiceProvider.GetRequiredService<ISender>();
        var receipt = await sender2.Send(new GetReceiptByTokenQuery(token));
        Assert.Equal(5000m, receipt!.ChangeAmount);
        Assert.Equal(total + 5000m, receipt.PaidCash);
    }

    [Fact]
    public async Task Card_overpay_throws()
    {
        var (branch1, warehouse1, businessId, adminId, variantId, price) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await TestShift.OpenAsync(Fixture);

        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            sender.Send(new CreateSaleCommand(warehouse1, null, 0, price * 2 + 1000m, 0, [new CreateSaleItemDto(variantId, 2)])));
    }
}
