using Cartex.Application.Suppliers.Commands;
using Cartex.Application.Supplies.Commands;
using Cartex.Application.Shifts.Commands;
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
public class SupplierDebtTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private async Task<(long branch1, long warehouse1, long businessId, long adminId, long variantId)> SetupAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branch1 = (await db.Branches.FirstAsync(b => b.Name == "Asosiy filial")).Id;
        var warehouse1 = (await db.Warehouses.FirstAsync(w => w.Name == "Asosiy ombor")).Id;
        var businessId = (await db.Businesses.FirstAsync()).Id;
        var adminId = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;
        var productId = (await db.Products.FirstAsync(p => p.Name == "Smesitel oshxona Zegor")).Id;
        var variantId = (await db.ProductVariants.FirstAsync(v => v.ProductId == productId)).Id;
        return (branch1, warehouse1, businessId, adminId, variantId);
    }

    private async Task<long> CreateSupplierAsync()
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(new CreateSupplierCommand("Test Ta'minotchi", null));
    }

    private async Task<decimal> PayableAsync(long supplierId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var balance = await db.Accounts.Where(a => a.SupplierId == supplierId && a.Type == AccountType.Debt)
            .Select(a => (decimal?)a.Balance).FirstOrDefaultAsync() ?? 0;
        return -balance;
    }

    [Fact]
    public async Task Supply_with_partial_cash_payment_posts_ledger_and_tracks_payable()
    {
        var (branch1, warehouse1, businessId, adminId, variantId) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        var shiftId = await TestShift.OpenAsync(Fixture);
        var supplierId = await CreateSupplierAsync();

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new AddCashMovementCommand(30_000m, IsPayOut: false));
        }

        decimal cashBefore;
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            cashBefore = await db.Accounts.Where(a => a.BranchId == branch1 && a.Type == AccountType.Cash)
                .Select(a => a.Balance).FirstOrDefaultAsync();

            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new CreateSupplyCommand(supplierId, warehouse1, DateOnly.FromDateTime(DateTime.Today),
                [new CreateSupplyItemDto(variantId, 10, 8000m, null)], PaidCash: 30_000m));
        }

        Assert.Equal(50_000m, await PayableAsync(supplierId));

        using var check = Fixture.CreateScope();
        var db2 = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var cashAfter = (await db2.Accounts.FirstAsync(a => a.BranchId == branch1 && a.Type == AccountType.Cash)).Balance;
        Assert.Equal(cashBefore - 30_000m, cashAfter);

        var sender2 = check.ServiceProvider.GetRequiredService<ISender>();
        var report = await sender2.Send(new CloseShiftCommand(shiftId, 0));
        Assert.Equal(30_000m, report.SupplyPayOut);
        Assert.Equal(0m, report.ExpectedCash);
    }

    [Fact]
    public async Task Pay_supplier_debt_cash_requires_shift_then_clears_payable()
    {
        var (branch1, warehouse1, businessId, adminId, variantId) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        var supplierId = await CreateSupplierAsync();

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new CreateSupplyCommand(supplierId, warehouse1, DateOnly.FromDateTime(DateTime.Today),
                [new CreateSupplyItemDto(variantId, 5, 8000m, null)]));
        }

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                sender.Send(new PaySupplierDebtCommand(supplierId, 10_000m)));
        }

        await TestShift.OpenAsync(Fixture);
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new AddCashMovementCommand(40_000m, IsPayOut: false));
        }

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new PaySupplierDebtCommand(supplierId, 40_000m));
        }

        Assert.Equal(0m, await PayableAsync(supplierId));
    }

    [Fact]
    public async Task Overpaying_supplier_debt_creates_advance()
    {
        var (branch1, warehouse1, businessId, adminId, variantId) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await TestShift.OpenAsync(Fixture);
        var supplierId = await CreateSupplierAsync();

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new CreateSupplyCommand(supplierId, warehouse1, DateOnly.FromDateTime(DateTime.Today),
                [new CreateSupplyItemDto(variantId, 5, 8000m, null)]));
        }

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new AddCashMovementCommand(50_000m, IsPayOut: false));
        }

        using var scope2 = Fixture.CreateScope();
        var sender2 = scope2.ServiceProvider.GetRequiredService<ISender>();
        await sender2.Send(new PaySupplierDebtCommand(supplierId, 50_000m));

        Assert.Equal(-10_000m, await PayableAsync(supplierId));
    }

    [Theory]
    [InlineData(AccountType.Transfer)]
    [InlineData(AccountType.Bank)]
    public async Task Paying_supplier_debt_by_transfer_uses_own_account_without_shift(AccountType method)
    {
        var (branch1, warehouse1, businessId, adminId, variantId) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        var supplierId = await CreateSupplierAsync();

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new CreateSupplyCommand(supplierId, warehouse1, DateOnly.FromDateTime(DateTime.Today),
                [new CreateSupplyItemDto(variantId, 5, 8000m, null)]));
        }

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new PaySupplierDebtCommand(supplierId, 40_000m, method));
        }

        Assert.Equal(0m, await PayableAsync(supplierId));

        using var check = Fixture.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var balance = await db.Accounts
            .Where(a => a.BranchId == branch1 && a.Type == method)
            .Select(a => a.Balance)
            .SingleAsync();

        Assert.Equal(-40_000m, balance);
        Assert.Equal(0m, await db.Accounts.Where(a => a.BranchId == branch1 && a.Type == AccountType.Cash).Select(a => a.Balance).SingleAsync());
    }

    // SMENA-08
    [Fact]
    public async Task Paying_supplier_debt_cash_from_insufficient_till_is_rejected()
    {
        var (branch1, warehouse1, businessId, adminId, variantId) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await TestShift.OpenAsync(Fixture, 100_000m);
        var supplierId = await CreateSupplierAsync();

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new CreateSupplyCommand(supplierId, warehouse1, DateOnly.FromDateTime(DateTime.Today),
                [new CreateSupplyItemDto(variantId, 5, 8000m, null)]));
        }

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                sender.Send(new PaySupplierDebtCommand(supplierId, 10_000m)));
            Assert.Equal("cash_balance_insufficient", error.Code);
        }

        Assert.Equal(40_000m, await PayableAsync(supplierId));

        using var check = Fixture.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(0m, await db.Accounts.Where(a => a.BranchId == branch1 && a.Type == AccountType.Cash)
            .Select(a => (decimal?)a.Balance).FirstOrDefaultAsync() ?? 0m);
        Assert.False(await db.Transactions.AnyAsync(t =>
            t.OperationType == OperationType.SupplyPay || t.OperationType == OperationType.DebtPay));
    }
}
