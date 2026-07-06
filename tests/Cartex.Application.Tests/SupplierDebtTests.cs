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
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branch1 = (await db.Branches.FirstAsync(b => b.Name == "Filial 1")).Id;
        var warehouse1 = (await db.Warehouses.FirstAsync(w => w.Name == "Filial 1 ombori")).Id;
        var businessId = (await db.Businesses.FirstAsync()).Id;
        var adminId = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;
        var productId = (await db.Products.FirstAsync(p => p.Name == "Coca-Cola 1.5L")).Id;
        var variantId = (await db.ProductVariants.FirstAsync(v => v.ProductId == productId)).Id;
        return (branch1, warehouse1, businessId, adminId, variantId);
    }

    private async Task<long> CreateSupplierAsync()
    {
        using var scope = fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(new CreateSupplierCommand("Test Ta'minotchi", null));
    }

    private async Task<decimal> PayableAsync(long supplierId)
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var balance = await db.Accounts.Where(a => a.SupplierId == supplierId && a.Type == AccountType.Debt)
            .Select(a => (decimal?)a.Balance).FirstOrDefaultAsync() ?? 0;
        return -balance;
    }

    [Fact]
    public async Task Supply_with_partial_cash_payment_posts_ledger_and_tracks_payable()
    {
        var (branch1, warehouse1, businessId, adminId, variantId) = await SetupAsync();
        fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        var shiftId = await TestShift.OpenAsync(fixture);
        var supplierId = await CreateSupplierAsync();

        decimal cashBefore;
        using (var scope = fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            cashBefore = await db.Accounts.Where(a => a.BranchId == branch1 && a.Type == AccountType.Cash)
                .Select(a => a.Balance).FirstOrDefaultAsync();

            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new CreateSupplyCommand(supplierId, warehouse1, DateOnly.FromDateTime(DateTime.Today),
                [new CreateSupplyItemDto(variantId, 10, 8000m, null)], PaidCash: 30_000m));
        }

        Assert.Equal(50_000m, await PayableAsync(supplierId));

        using var check = fixture.CreateScope();
        var db2 = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var cashAfter = (await db2.Accounts.FirstAsync(a => a.BranchId == branch1 && a.Type == AccountType.Cash)).Balance;
        Assert.Equal(cashBefore - 30_000m, cashAfter);

        var sender2 = check.ServiceProvider.GetRequiredService<ISender>();
        var report = await sender2.Send(new CloseShiftCommand(shiftId, 0));
        Assert.Equal(30_000m, report.SupplyPayOut);
        Assert.Equal(-30_000m, report.ExpectedCash);
    }

    [Fact]
    public async Task Pay_supplier_debt_cash_requires_shift_then_clears_payable()
    {
        var (branch1, warehouse1, businessId, adminId, variantId) = await SetupAsync();
        fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        var supplierId = await CreateSupplierAsync();

        using (var scope = fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new CreateSupplyCommand(supplierId, warehouse1, DateOnly.FromDateTime(DateTime.Today),
                [new CreateSupplyItemDto(variantId, 5, 8000m, null)]));
        }

        using (var scope = fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                sender.Send(new PaySupplierDebtCommand(supplierId, 10_000m, false)));
        }

        await TestShift.OpenAsync(fixture);
        using (var scope = fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new PaySupplierDebtCommand(supplierId, 40_000m, false));
        }

        Assert.Equal(0m, await PayableAsync(supplierId));
    }

    [Fact]
    public async Task Overpaying_supplier_debt_throws()
    {
        var (branch1, warehouse1, businessId, adminId, variantId) = await SetupAsync();
        fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await TestShift.OpenAsync(fixture);
        var supplierId = await CreateSupplierAsync();

        using (var scope = fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new CreateSupplyCommand(supplierId, warehouse1, DateOnly.FromDateTime(DateTime.Today),
                [new CreateSupplyItemDto(variantId, 5, 8000m, null)]));
        }

        using var scope2 = fixture.CreateScope();
        var sender2 = scope2.ServiceProvider.GetRequiredService<ISender>();
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            sender2.Send(new PaySupplierDebtCommand(supplierId, 40_001m, false)));

        Assert.Equal(40_000m, await PayableAsync(supplierId));
    }
}
