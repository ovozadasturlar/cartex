using Cartex.Application.Products.Commands;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Shifts.Commands;
using Cartex.Application.Supplies.Commands;
using Cartex.Application.Supplies.Queries;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class DeleteSupplyTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private async Task<(long branch1, long warehouse1, long businessId, long adminId, long variantId, long supplierId)> SetupAsync()
    {
        long branch1, warehouse1, businessId, adminId, supplierId;
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            branch1 = (await db.Branches.FirstAsync(b => b.Name == "Asosiy filial")).Id;
            warehouse1 = (await db.Warehouses.FirstAsync(w => w.Name == "Asosiy ombor")).Id;
            businessId = (await db.Businesses.FirstAsync()).Id;
            adminId = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;

            var supplier = new Supplier { Name = "Test Supplier" };
            db.Suppliers.Add(supplier);
            await db.SaveChangesAsync();
            supplierId = supplier.Id;
        }

        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);

        long variantId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var unitId = (await db.Units.FirstAsync(u => u.ShortName == "dona")).Id;
            var productId = await sender.Send(new CreateProductCommand("Bekor mahsulot", null, unitId, 0, null));
            variantId = (await db.ProductVariants.FirstAsync(v => v.ProductId == productId)).Id;
        }

        return (branch1, warehouse1, businessId, adminId, variantId, supplierId);
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
    public async Task Delete_supply_restores_stock_debt_and_cash()
    {
        var (branch1, warehouse1, _, _, variantId, supplierId) = await SetupAsync();
        var shiftId = await TestShift.OpenAsync(Fixture);

        decimal cashBefore;
        long supplyId;
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            cashBefore = await db.Accounts.Where(a => a.BranchId == branch1 && a.Type == AccountType.Cash)
                .Select(a => a.Balance).FirstOrDefaultAsync();

            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            supplyId = await sender.Send(new CreateSupplyCommand(supplierId, warehouse1, DateOnly.FromDateTime(DateTime.Today),
                [new CreateSupplyItemDto(variantId, 10, 8000m, null)], PaidCash: 30_000m));
        }

        Assert.Equal(50_000m, await PayableAsync(supplierId));

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var detail = await sender.Send(new GetSupplyByIdQuery(supplyId));
            Assert.NotNull(detail);
            Assert.Equal(80_000m, detail!.TotalAmount);
            Assert.Equal(30_000m, detail.PaidCash);
            Assert.Equal(0m, detail.PaidCard);
            var item = Assert.Single(detail.Items);
            Assert.Equal(10, item.Quantity);
            Assert.Equal(8000m, item.PurchasePrice);
        }

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new DeleteSupplyCommand(supplyId));
        }

        Assert.Equal(0m, await PayableAsync(supplierId));

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Null(await db.Supplies.FirstOrDefaultAsync(s => s.Id == supplyId));
            Assert.Empty(await db.Stocks.Where(s => s.SupplyId == supplyId).ToListAsync());
            var cashAfter = (await db.Accounts.FirstAsync(a => a.BranchId == branch1 && a.Type == AccountType.Cash)).Balance;
            Assert.Equal(cashBefore, cashAfter);
        }

        using var check = Fixture.CreateScope();
        var sender2 = check.ServiceProvider.GetRequiredService<ISender>();
        var report = await sender2.Send(new CloseShiftCommand(shiftId, 0));
        Assert.Equal(0m, report.SupplyPayOut);
        Assert.Equal(0m, report.ExpectedCash);
    }

    [Fact]
    public async Task Delete_supply_rejected_after_partial_sale()
    {
        var (_, warehouse1, _, _, variantId, supplierId) = await SetupAsync();
        await TestShift.OpenAsync(Fixture);

        long supplyId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            supplyId = await sender.Send(new CreateSupplyCommand(supplierId, warehouse1, DateOnly.FromDateTime(DateTime.Today),
                [new CreateSupplyItemDto(variantId, 10, 8000m, null, SellingPrice: 12_000m)]));
        }

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new CreateSaleCommand(warehouse1, null, 12_000m, 0, 0,
                [new CreateSaleItemDto(variantId, 1)]));
        }

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await Assert.ThrowsAsync<BusinessRuleException>(() => sender.Send(new DeleteSupplyCommand(supplyId)));
        }

        Assert.Equal(80_000m, await PayableAsync(supplierId));
    }
}
