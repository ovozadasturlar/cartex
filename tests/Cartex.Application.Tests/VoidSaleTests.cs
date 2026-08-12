using Cartex.Application.Common.Messaging;
using Cartex.Application.CustomerReturns.Commands;
using Cartex.Application.Customers.Commands;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class VoidSaleTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private async Task<(long BranchId, long WarehouseId, long BusinessId, long AdminId, long VariantId)> SetupAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branchId = (await db.Branches.FirstAsync(x => x.Name == "Asosiy filial")).Id;
        var warehouseId = (await db.Warehouses.FirstAsync(x => x.Name == "Asosiy ombor")).Id;
        var businessId = (await db.Businesses.FirstAsync()).Id;
        var adminId = (await db.Users.FirstAsync(x => x.Username == "admin")).Id;
        var productId = (await db.Products.FirstAsync(x => x.Name == "Smesitel oshxona Zegor")).Id;
        var variantId = (await db.ProductVariants.FirstAsync(x => x.ProductId == productId)).Id;
        return (branchId, warehouseId, businessId, adminId, variantId);
    }

    private async Task<decimal> StockAsync(long warehouseId, long variantId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Stocks.Where(x => x.WarehouseId == warehouseId && x.VariantId == variantId)
            .SumAsync(x => x.Quantity);
    }

    private async Task<decimal> CashAsync(long branchId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Accounts.Where(x => x.BranchId == branchId && x.Type == AccountType.Cash)
            .SumAsync(x => x.Balance);
    }

    [Fact]
    public async Task Void_restores_stock_and_cash_and_marks_the_sale()
    {
        var setup = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(setup.AdminId, setup.BusinessId, setup.BranchId);
        await TestShift.OpenAsync(Fixture);

        var stockBefore = await StockAsync(setup.WarehouseId, setup.VariantId);
        var cashBefore = await CashAsync(setup.BranchId);

        long saleId;
        using (var scope = Fixture.CreateScope())
        {
            saleId = (await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                new CreateSaleCommand(setup.WarehouseId, null, 770_000m, 0, 0,
                    [new CreateSaleItemDto(setup.VariantId, 2)]))).SaleId;
        }

        Assert.NotEqual(stockBefore, await StockAsync(setup.WarehouseId, setup.VariantId));
        Assert.NotEqual(cashBefore, await CashAsync(setup.BranchId));

        using (var scope = Fixture.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                new VoidSaleCommand(saleId, "Chegirma yozilmagan"));
        }

        Assert.Equal(stockBefore, await StockAsync(setup.WarehouseId, setup.VariantId));
        Assert.Equal(cashBefore, await CashAsync(setup.BranchId));

        using var check = Fixture.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sale = await db.Sales.FirstAsync(x => x.Id == saleId);
        Assert.Equal(SaleStatus.Voided, sale.Status);
        Assert.NotNull(sale.VoidedAt);
        Assert.Equal("Chegirma yozilmagan", sale.VoidReason);
        Assert.NotNull(sale.ShiftId);
    }

    [Fact]
    public async Task Void_clears_the_customer_debt_it_created()
    {
        var setup = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(setup.AdminId, setup.BusinessId, setup.BranchId);
        await TestShift.OpenAsync(Fixture);

        long customerId;
        using (var scope = Fixture.CreateScope())
        {
            customerId = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                new CreateCustomerCommand("Storno mijozi", "+998" + Random.Shared.NextInt64(100_000_000, 999_999_999),
                    null, 0m, CreditLimit: 100_000_000m));
        }

        long saleId;
        using (var scope = Fixture.CreateScope())
        {
            saleId = (await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                new CreateSaleCommand(setup.WarehouseId, customerId, 0, 0, 0,
                    [new CreateSaleItemDto(setup.VariantId, 1)],
                    DebtDueDate: DateOnly.FromDateTime(DateTime.Today.AddDays(30))))).SaleId;
        }

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var debt = await db.Accounts.Where(x => x.CustomerId == customerId && x.Type == AccountType.Debt)
                .SumAsync(x => x.Balance);
            Assert.True(debt > 0);
        }

        using (var scope = Fixture.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                new VoidSaleCommand(saleId, "Noto'g'ri mijoz"));
        }

        using var check = Fixture.CreateScope();
        var verify = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(0m, await verify.Accounts
            .Where(x => x.CustomerId == customerId && x.Type == AccountType.Debt)
            .SumAsync(x => x.Balance));
    }

    [Fact]
    public async Task Void_is_rejected_once_the_sale_has_been_returned()
    {
        var setup = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(setup.AdminId, setup.BusinessId, setup.BranchId);
        await TestShift.OpenAsync(Fixture);

        long saleId;
        using (var scope = Fixture.CreateScope())
        {
            saleId = (await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                new CreateSaleCommand(setup.WarehouseId, null, 385_000m, 0, 0,
                    [new CreateSaleItemDto(setup.VariantId, 1)]))).SaleId;
        }

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                await TestReturns.ForSaleAsync(db, saleId));
        }

        using var check = Fixture.CreateScope();
        var sender = check.ServiceProvider.GetRequiredService<ISender>();
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            sender.Send(new VoidSaleCommand(saleId, "Kech qoldi")));
        Assert.Equal("sale_not_voidable", error.Code);
    }

    [Fact]
    public async Task Void_twice_is_rejected()
    {
        var setup = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(setup.AdminId, setup.BusinessId, setup.BranchId);
        await TestShift.OpenAsync(Fixture);

        long saleId;
        using (var scope = Fixture.CreateScope())
        {
            saleId = (await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                new CreateSaleCommand(setup.WarehouseId, null, 385_000m, 0, 0,
                    [new CreateSaleItemDto(setup.VariantId, 1)]))).SaleId;
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                new VoidSaleCommand(saleId, "Birinchi"));
        }

        using var check = Fixture.CreateScope();
        var sender = check.ServiceProvider.GetRequiredService<ISender>();
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            sender.Send(new VoidSaleCommand(saleId, "Ikkinchi")));
        Assert.Equal("sale_not_voidable", error.Code);
    }
}
