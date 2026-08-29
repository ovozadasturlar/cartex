using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Application.Products.Commands;
using Cartex.Application.StockWriteOffs.Commands;
using Cartex.Application.StockWriteOffs.Queries;
using Cartex.Application.Suppliers.Commands;
using Cartex.Application.Supplies.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Shared.Models.Stocks;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class StockWriteOffTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private sealed record Setup(long BranchId, long WarehouseId, long BusinessId, long AdminId, long SellerId);

    private async Task<Setup> SetupAsync(bool trackWriteOff = true)
    {
        Setup setup;
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            setup = new Setup(
                await db.Branches.Where(x => x.Name == "Asosiy filial").Select(x => x.Id).FirstAsync(),
                await db.Warehouses.Where(x => x.Name == "Asosiy ombor").Select(x => x.Id).FirstAsync(),
                await db.Businesses.Select(x => x.Id).FirstAsync(),
                await db.Users.Where(x => x.Username == "admin").Select(x => x.Id).FirstAsync(),
                await db.Users.Where(x => x.Username == "seller").Select(x => x.Id).FirstAsync());
        }

        Fixture.CurrentUser.AsAdmin(setup.AdminId, setup.BusinessId, setup.BranchId);
        await TestShift.OpenAsync(Fixture);
        await TrackWriteOffAsync(trackWriteOff);
        return setup;
    }

    private async Task TrackWriteOffAsync(bool enabled)
    {
        using var scope = Fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISettingsService>()
            .SetAsync(SettingKeys.SalesPolicy, new SalesPolicySettings { TrackWriteOff = enabled });
    }

    private async Task<long> CreateVariantAsync(string name)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var unitId = await db.Units.Where(x => x.IsDefault).Select(x => x.Id).FirstAsync();
        var productId = await sender.Send(new CreateProductCommand(name, null, unitId, null, null, SellingPrice: 20_000m));
        return await db.ProductVariants.Where(x => x.ProductId == productId).Select(x => x.Id).SingleAsync();
    }

    private async Task<long> CreateSupplierAsync(string name, bool acceptsReturns)
    {
        using var scope = Fixture.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new CreateSupplierCommand(name, null, acceptsReturns));
    }

    private async Task<long> ReceiveAsync(long? supplierId, long warehouseId, long variantId, decimal quantity, decimal purchasePrice)
    {
        using var scope = Fixture.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new CreateSupplyCommand(supplierId, warehouseId, DateOnly.FromDateTime(DateTime.Today),
                [new CreateSupplyItemDto(variantId, quantity, purchasePrice, null)]));
    }

    private async Task<long> BatchIdAsync(long supplyId, long variantId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Stocks.Where(x => x.SupplyId == supplyId && x.VariantId == variantId).Select(x => x.Id).SingleAsync();
    }

    private async Task<StockWriteOffCreatedDto> WriteOffAsync(long warehouseId, StockWriteOffLineInput line, string? note = null)
    {
        using var scope = Fixture.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new CreateStockWriteOffCommand(warehouseId, [line], Note: note));
    }

    private async Task<decimal> StockAsync(long warehouseId, long variantId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Stocks.Where(x => x.WarehouseId == warehouseId && x.VariantId == variantId).SumAsync(x => x.Quantity);
    }

    private async Task<List<InventoryMovement>> MovementsAsync(long warehouseId, long variantId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.InventoryMovements.AsNoTracking()
            .Where(x => x.WarehouseId == warehouseId && x.VariantId == variantId)
            .OrderBy(x => x.Id)
            .ToListAsync();
    }

    private async Task<decimal> PayableAsync(long supplierId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return -(await db.Accounts.Where(x => x.SupplierId == supplierId && x.Type == AccountType.Debt)
            .Select(x => (decimal?)x.Balance).FirstOrDefaultAsync() ?? 0m);
    }

    private async Task<List<(long Id, decimal Balance)>> AccountsAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Accounts.AsNoTracking().OrderBy(x => x.Id)
            .Select(x => new ValueTuple<long, decimal>(x.Id, x.Balance)).ToListAsync();
    }

    private async Task<int> TransactionCountAsync()
    {
        using var scope = Fixture.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Transactions.CountAsync();
    }

    [Fact]
    public async Task BRAK_01_Reason_comes_from_a_fixed_list_and_free_text_cannot_replace_it()
    {
        var setup = await SetupAsync();
        var variantId = await CreateVariantAsync("Brak sabab ro'yxati");
        await ReceiveAsync(null, setup.WarehouseId, variantId, 10m, 5_000m);

        Assert.Equal(["Broken", "Expired", "Lost", "Stolen"], Enum.GetNames<StockWriteOffReason>());
        Assert.DoesNotContain(typeof(StockWriteOffLine).GetProperties(),
            x => x.PropertyType == typeof(string) && x.Name.Contains("Reason", StringComparison.OrdinalIgnoreCase));

        var transactionsBefore = await TransactionCountAsync();
        var error = await Assert.ThrowsAnyAsync<Exception>(() => WriteOffAsync(setup.WarehouseId,
            new StockWriteOffLineInput(variantId, 2m, (StockWriteOffReason)99, Note: "sichqon kemirdi")));
        Assert.True(error is ValidationException or BusinessRuleException, error.GetType().Name);

        Assert.Equal(10m, await StockAsync(setup.WarehouseId, variantId));
        Assert.DoesNotContain(await MovementsAsync(setup.WarehouseId, variantId), x => x.Kind == InventoryMovementKind.WriteOff);
        Assert.Equal(transactionsBefore, await TransactionCountAsync());

        using (var scope = Fixture.CreateScope())
            Assert.Empty(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().StockWriteOffDocuments.ToListAsync());

        await WriteOffAsync(setup.WarehouseId, new StockWriteOffLineInput(variantId, 2m, StockWriteOffReason.Expired, Note: "sichqon kemirdi"));

        using var check = Fixture.CreateScope();
        var sender = check.ServiceProvider.GetRequiredService<ISender>();
        var expired = await sender.Send(new GetStockWriteOffsQuery(Reason: StockWriteOffReason.Expired));
        var broken = await sender.Send(new GetStockWriteOffsQuery(Reason: StockWriteOffReason.Broken));

        Assert.Equal(nameof(StockWriteOffReason.Expired), Assert.Single(Assert.Single(expired).Lines).Reason);
        Assert.Empty(broken);
    }

    [Fact]
    public async Task BRAK_02_Writing_off_two_of_ten_leaves_eight_and_moves_them_to_Scrap_with_one_movement()
    {
        var setup = await SetupAsync();
        var variantId = await CreateVariantAsync("Brak ikki dona");
        await ReceiveAsync(null, setup.WarehouseId, variantId, 10m, 5_000m);
        Assert.Equal(10m, await StockAsync(setup.WarehouseId, variantId));

        await WriteOffAsync(setup.WarehouseId, new StockWriteOffLineInput(variantId, 2m, StockWriteOffReason.Broken));

        var stock = await StockAsync(setup.WarehouseId, variantId);
        Assert.Equal(8m, stock);

        var movements = await MovementsAsync(setup.WarehouseId, variantId);
        var written = Assert.Single(movements, x => x.Kind == InventoryMovementKind.WriteOff);
        Assert.Equal(-2m, written.Quantity);
        Assert.Equal(InventoryLocationKind.Warehouse, written.FromLocationKind);
        Assert.Equal(InventoryLocationKind.Scrap, written.ToLocationKind);
        Assert.Equal(stock, movements.Sum(x => x.Quantity));

        var scrapped = -movements
            .Where(x => x.ToLocationKind == InventoryLocationKind.Scrap || x.FromLocationKind == InventoryLocationKind.Scrap)
            .Sum(x => x.Quantity);
        Assert.Equal(2m, scrapped);

        using var scope = Fixture.CreateScope();
        var balances = await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new GetWriteOffBalancesQuery(setup.WarehouseId, variantId));
        var scrap = Assert.Single(balances, x => x.Location == nameof(InventoryLocationKind.Scrap));
        Assert.Equal(2m, scrap.Quantity);
    }

    [Fact]
    public async Task BRAK_03_BRAK_04_Supplier_claim_is_allowed_only_for_a_batch_whose_supplier_accepts_returns()
    {
        var setup = await SetupAsync();
        var variantId = await CreateVariantAsync("Brak partiya ta'minotchisi");
        var accepting = await CreateSupplierAsync("Qaytarishni qabul qiladi", true);
        var refusing = await CreateSupplierAsync("Qaytarishni qabul qilmaydi", false);

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var plain = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new CreateSupplierCommand("Standart ta'minotchi", null));
            Assert.False(await db.Suppliers.Where(x => x.Id == plain).Select(x => x.AcceptsReturns).SingleAsync());
        }

        var batchA = await BatchIdAsync(await ReceiveAsync(accepting, setup.WarehouseId, variantId, 5m, 10_000m), variantId);
        var batchB = await BatchIdAsync(await ReceiveAsync(refusing, setup.WarehouseId, variantId, 5m, 10_000m), variantId);

        using (var scope = Fixture.CreateScope())
        {
            var batches = await scope.ServiceProvider.GetRequiredService<ISender>()
                .Send(new GetWriteOffBatchesQuery(setup.WarehouseId, variantId));
            Assert.True(Assert.Single(batches, x => x.StockId == batchA).SupplierAcceptsReturns);
            Assert.False(Assert.Single(batches, x => x.StockId == batchB).SupplierAcceptsReturns);
        }

        await WriteOffAsync(setup.WarehouseId,
            new StockWriteOffLineInput(variantId, 1m, StockWriteOffReason.Broken, InventoryDisposition.SupplierClaim, batchA));

        var claim = Assert.Single(await MovementsAsync(setup.WarehouseId, variantId),
            x => x.ToLocationKind == InventoryLocationKind.SupplierClaim);
        Assert.Equal(-1m, claim.Quantity);
        Assert.Equal(batchA, claim.StockId);

        await Assert.ThrowsAnyAsync<Exception>(() => WriteOffAsync(setup.WarehouseId,
            new StockWriteOffLineInput(variantId, 1m, StockWriteOffReason.Broken, InventoryDisposition.SupplierClaim, batchB)));

        var movements = await MovementsAsync(setup.WarehouseId, variantId);
        Assert.DoesNotContain(movements, x => x.StockId == batchB && x.Kind == InventoryMovementKind.WriteOff);
        Assert.DoesNotContain(movements, x => x.ToLocationKind == InventoryLocationKind.Scrap);

        using var check = Fixture.CreateScope();
        var db2 = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(5m, await db2.Stocks.Where(x => x.Id == batchB).Select(x => x.Quantity).SingleAsync());
        Assert.Equal(1, await db2.StockWriteOffDocuments.CountAsync());
    }

    [Fact]
    public async Task BRAK_05_Supplier_claim_reduces_that_suppliers_debt()
    {
        var setup = await SetupAsync();
        var variantId = await CreateVariantAsync("Brak ta'minotchi hisobi");
        var supplierId = await CreateSupplierAsync("Da'vo ta'minotchisi", true);
        var other = await CreateSupplierAsync("Boshqa ta'minotchi", true);
        var batchId = await BatchIdAsync(await ReceiveAsync(supplierId, setup.WarehouseId, variantId, 20m, 10_000m), variantId);
        await ReceiveAsync(other, setup.WarehouseId, await CreateVariantAsync("Boshqa mahsulot"), 5m, 10_000m);

        Assert.Equal(200_000m, await PayableAsync(supplierId));
        var otherBefore = await PayableAsync(other);

        var created = await WriteOffAsync(setup.WarehouseId,
            new StockWriteOffLineInput(variantId, 10m, StockWriteOffReason.Broken, InventoryDisposition.SupplierClaim, batchId));

        Assert.Equal(100_000m, created.SupplierClaimAmount);
        Assert.Equal(100_000m, await PayableAsync(supplierId));
        Assert.Equal(otherBefore, await PayableAsync(other));
    }

    [Fact]
    public async Task BRAK_05_Plain_scrap_leaves_every_account_untouched()
    {
        var setup = await SetupAsync();
        var variantId = await CreateVariantAsync("Brak sof zarar");
        var supplierId = await CreateSupplierAsync("Brak ta'minotchisi", true);
        await ReceiveAsync(supplierId, setup.WarehouseId, variantId, 10m, 10_000m);

        var accountsBefore = await AccountsAsync();
        var transactionsBefore = await TransactionCountAsync();
        var payableBefore = await PayableAsync(supplierId);

        await WriteOffAsync(setup.WarehouseId, new StockWriteOffLineInput(variantId, 2m, StockWriteOffReason.Stolen));

        Assert.Equal(8m, await StockAsync(setup.WarehouseId, variantId));
        Assert.Equal(accountsBefore, await AccountsAsync());
        Assert.Equal(transactionsBefore, await TransactionCountAsync());
        Assert.Equal(payableBefore, await PayableAsync(supplierId));
    }

    [Fact]
    public async Task BRAK_06_Write_off_is_refused_while_the_policy_is_off_and_enabling_it_changes_no_balance()
    {
        var setup = await SetupAsync(trackWriteOff: false);
        var variantId = await CreateVariantAsync("Brak siyosati");
        await ReceiveAsync(null, setup.WarehouseId, variantId, 10m, 5_000m);

        var line = new StockWriteOffLineInput(variantId, 2m, StockWriteOffReason.Lost);
        await Assert.ThrowsAsync<ForbiddenException>(() => WriteOffAsync(setup.WarehouseId, line));

        var stock = await StockAsync(setup.WarehouseId, variantId);
        var movements = await MovementsAsync(setup.WarehouseId, variantId);
        var accounts = await AccountsAsync();
        var transactions = await TransactionCountAsync();

        Assert.Equal(10m, stock);
        Assert.DoesNotContain(movements, x => x.Kind == InventoryMovementKind.WriteOff);

        await TrackWriteOffAsync(true);

        Assert.Equal(stock, await StockAsync(setup.WarehouseId, variantId));
        Assert.Equal(movements.Count, (await MovementsAsync(setup.WarehouseId, variantId)).Count);
        Assert.Equal(accounts, await AccountsAsync());
        Assert.Equal(transactions, await TransactionCountAsync());

        Fixture.CurrentUser.AsCashier(setup.SellerId, setup.BusinessId, setup.BranchId);
        await Assert.ThrowsAsync<ForbiddenException>(() => WriteOffAsync(setup.WarehouseId, line));

        Fixture.CurrentUser.Granted.Add(AppPermissions.Stocks.WriteOff);
        await WriteOffAsync(setup.WarehouseId, line);

        Assert.Equal(8m, await StockAsync(setup.WarehouseId, variantId));
    }

    [Fact]
    public async Task BRAK_07_Write_off_cannot_be_edited_or_deleted_and_is_corrected_by_a_reverse_keeping_both_movements()
    {
        var setup = await SetupAsync();
        var variantId = await CreateVariantAsync("Brak teskari amal");
        await ReceiveAsync(null, setup.WarehouseId, variantId, 10m, 5_000m);

        var created = await WriteOffAsync(setup.WarehouseId, new StockWriteOffLineInput(variantId, 2m, StockWriteOffReason.Broken));
        var written = Assert.Single(await MovementsAsync(setup.WarehouseId, variantId), x => x.Kind == InventoryMovementKind.WriteOff);

        Assert.DoesNotContain(typeof(CreateStockWriteOffCommand).Assembly.GetTypes(),
            x => x.Name.StartsWith("UpdateStockWriteOff") || x.Name.StartsWith("DeleteStockWriteOff"));

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.InventoryMovements.SingleAsync(x => x.Id == written.Id)).Quantity = -1m;
            await Assert.ThrowsAnyAsync<Exception>(() => db.SaveChangesAsync());
        }

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.InventoryMovements.Remove(await db.InventoryMovements.SingleAsync(x => x.Id == written.Id));
            await Assert.ThrowsAnyAsync<Exception>(() => db.SaveChangesAsync());
        }

        using (var scope = Fixture.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ReverseStockWriteOffCommand(created.Id, "xato chiqim"));

        var stock = await StockAsync(setup.WarehouseId, variantId);
        var movements = await MovementsAsync(setup.WarehouseId, variantId);

        Assert.Equal(10m, stock);
        Assert.Equal(-2m, Assert.Single(movements, x => x.Id == written.Id).Quantity);
        Assert.Equal([-2m, 2m], movements.Where(x => x.Kind == InventoryMovementKind.WriteOff).Select(x => x.Quantity));
        Assert.Equal(stock, movements.Sum(x => x.Quantity));

        using var check = Fixture.CreateScope();
        var journal = await check.ServiceProvider.GetRequiredService<ISender>().Send(new GetStockWriteOffsQuery());
        Assert.Equal(2, journal.Count);
        Assert.Contains(journal, x => x.Id == created.Id);
        Assert.Contains(journal, x => x.ReversesDocumentId == created.Id);
    }

    [Fact]
    public async Task BRAK_07_A_write_off_cannot_be_reversed_twice()
    {
        var setup = await SetupAsync();
        var variantId = await CreateVariantAsync("Brak ikki marta teskari");
        await ReceiveAsync(null, setup.WarehouseId, variantId, 10m, 5_000m);

        var created = await WriteOffAsync(setup.WarehouseId, new StockWriteOffLineInput(variantId, 2m, StockWriteOffReason.Broken));

        long reversalId;
        using (var scope = Fixture.CreateScope())
            reversalId = (await scope.ServiceProvider.GetRequiredService<ISender>()
                .Send(new ReverseStockWriteOffCommand(created.Id))).Id;

        Assert.Equal(10m, await StockAsync(setup.WarehouseId, variantId));

        using (var scope = Fixture.CreateScope())
            await Assert.ThrowsAnyAsync<Exception>(() => scope.ServiceProvider.GetRequiredService<ISender>()
                .Send(new ReverseStockWriteOffCommand(created.Id)));

        using (var scope = Fixture.CreateScope())
            await Assert.ThrowsAnyAsync<Exception>(() => scope.ServiceProvider.GetRequiredService<ISender>()
                .Send(new ReverseStockWriteOffCommand(reversalId)));

        Assert.Equal(10m, await StockAsync(setup.WarehouseId, variantId));
        Assert.Equal(2, (await MovementsAsync(setup.WarehouseId, variantId))
            .Count(x => x.Kind == InventoryMovementKind.WriteOff));
    }

    [Fact]
    public async Task RUXSAT_A_write_off_from_another_branch_cannot_be_reversed()
    {
        var setup = await SetupAsync();
        var variantId = await CreateVariantAsync("Filiallararo teskari chiqim");
        await ReceiveAsync(null, setup.WarehouseId, variantId, 10m, 5_000m);
        var created = await WriteOffAsync(setup.WarehouseId, new StockWriteOffLineInput(variantId, 2m, StockWriteOffReason.Broken));

        long branch2, cashierId;
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            branch2 = await db.Branches.Where(b => b.Name == "Filial 2").Select(b => b.Id).FirstAsync();
            cashierId = await db.Users.Where(u => u.Username == "seller").Select(u => u.Id).FirstAsync();
        }

        Fixture.CurrentUser.AsCashier(cashierId, setup.BusinessId, branch2);
        Fixture.CurrentUser.Granted.Add(AppPermissions.Stocks.WriteOff);

        using (var scope2 = Fixture.CreateScope())
        {
            var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
                scope2.ServiceProvider.GetRequiredService<ISender>().Send(new ReverseStockWriteOffCommand(created.Id)));
            Assert.Equal("write_off_not_found", ex.Code);
        }

        Fixture.CurrentUser.AsAdmin(setup.AdminId, setup.BusinessId, setup.BranchId);
        Assert.Equal(8m, await StockAsync(setup.WarehouseId, variantId));
    }
}
