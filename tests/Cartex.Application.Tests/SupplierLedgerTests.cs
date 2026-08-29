using Cartex.Application.Suppliers.Commands;
using Cartex.Application.Suppliers.Queries;
using Cartex.Application.Supplies.Commands;
using Cartex.Application.Shifts.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class SupplierLedgerTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task Ledger_shows_payable_perspective_newest_first()
    {
        long branch1, warehouse1, businessId, adminId, variantId;
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            branch1 = (await db.Branches.FirstAsync(b => b.Name == "Asosiy filial")).Id;
            warehouse1 = (await db.Warehouses.FirstAsync(w => w.Name == "Asosiy ombor")).Id;
            businessId = (await db.Businesses.FirstAsync()).Id;
            adminId = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;
            var productId = (await db.Products.FirstAsync(p => p.Name == "Smesitel oshxona Zegor")).Id;
            variantId = (await db.ProductVariants.FirstAsync(v => v.ProductId == productId)).Id;
        }
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await TestShift.OpenAsync(Fixture);

        long supplierId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            supplierId = await sender.Send(new CreateSupplierCommand("Test Ta'minotchi", null));
        }

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new CreateSupplyCommand(supplierId, warehouse1, DateOnly.FromDateTime(DateTime.Today),
                [new CreateSupplyItemDto(variantId, 5, 8000m, null)]));
        }

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new AddCashMovementCommand(15_000m, IsPayOut: false));
        }

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new PaySupplierDebtCommand(supplierId, 15_000m));
        }

        using var check = Fixture.CreateScope();
        var checkSender = check.ServiceProvider.GetRequiredService<ISender>();
        var ledger = (await checkSender.Send(new GetSupplierLedgerQuery(supplierId))).ToList();

        Assert.Equal(2, ledger.Count);
        Assert.Equal("DebtPay", ledger[0].OperationType);
        Assert.Equal(-15_000m, ledger[0].Change);
        Assert.Equal(25_000m, ledger[0].BalanceAfter);
        Assert.Equal("DebtCharge", ledger[1].OperationType);
        Assert.Equal(40_000m, ledger[1].Change);
        Assert.Equal(40_000m, ledger[1].BalanceAfter);
    }
}
