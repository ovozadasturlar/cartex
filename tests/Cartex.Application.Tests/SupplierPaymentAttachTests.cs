using Cartex.Application.Suppliers.Commands;
using Cartex.Application.Suppliers.Queries;
using Cartex.Application.Supplies.Commands;
using Cartex.Application.Supplies.Queries;
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
public class SupplierPaymentAttachTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.Date);

    private async Task<(long warehouseId, long variantId)> SetupAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branch1 = (await db.Branches.FirstAsync(b => b.Name == "Asosiy filial")).Id;
        var warehouseId = (await db.Warehouses.FirstAsync(w => w.Name == "Asosiy ombor")).Id;
        var businessId = (await db.Businesses.FirstAsync()).Id;
        var adminId = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;
        var productId = (await db.Products.FirstAsync(p => p.Name == "Smesitel oshxona Zegor")).Id;
        var variantId = (await db.ProductVariants.FirstAsync(v => v.ProductId == productId)).Id;
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        return (warehouseId, variantId);
    }

    private async Task<long> CreateSupplierAsync(string name = "Test Ta'minotchi")
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(new CreateSupplierCommand(name, null));
    }

    private async Task<long> CreateSupplyAsync(long supplierId, long warehouseId, long variantId)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(new CreateSupplyCommand(supplierId, warehouseId, DateOnly.FromDateTime(DateTime.Today),
            [new CreateSupplyItemDto(variantId, 5, 8000m, null)]));
    }

    private async Task PayAsync(long supplierId, decimal amount)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(new PaySupplierDebtCommand(supplierId, amount, AccountType.Transfer));
    }

    private async Task<List<SupplierPaymentDto>> PaymentsAsync(long supplierId)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return [.. await sender.Send(new GetSupplierPaymentsQuery(supplierId, Today))];
    }

    private async Task AttachAsync(long supplyId, List<long> transactionIds)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(new AttachSupplierPaymentsCommand(supplyId, transactionIds));
    }

    private async Task<SupplyDetailDto> SupplyAsync(long supplyId)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return (await sender.Send(new GetSupplyByIdQuery(supplyId)))!;
    }

    [Fact]
    public async Task Payments_ListsOnlyUnattached()
    {
        var (warehouseId, variantId) = await SetupAsync();
        var supplierId = await CreateSupplierAsync();
        var supply1 = await CreateSupplyAsync(supplierId, warehouseId, variantId);
        await CreateSupplyAsync(supplierId, warehouseId, variantId);

        await PayAsync(supplierId, 40_000m);
        await PayAsync(supplierId, 40_000m);

        var before = await PaymentsAsync(supplierId);
        Assert.Equal(2, before.Count);
        Assert.All(before, p => Assert.Equal(40_000m, p.Amount));
        Assert.All(before, p => Assert.Equal(nameof(AccountType.Transfer), p.Method));

        var attached = before[0].TransactionId;
        await AttachAsync(supply1, [attached]);

        var after = await PaymentsAsync(supplierId);
        var remaining = Assert.Single(after);
        Assert.Equal(before[1].TransactionId, remaining.TransactionId);
    }

    [Fact]
    public async Task Attach_CountsIntoSupplyPaid()
    {
        var (warehouseId, variantId) = await SetupAsync();
        var supplierId = await CreateSupplierAsync();
        var supplyId = await CreateSupplyAsync(supplierId, warehouseId, variantId);

        await PayAsync(supplierId, 40_000m);

        Assert.Equal(0m, (await SupplyAsync(supplyId)).PaidTransfer);

        var payment = Assert.Single(await PaymentsAsync(supplierId));
        await AttachAsync(supplyId, [payment.TransactionId]);

        var supply = await SupplyAsync(supplyId);
        Assert.Equal(40_000m, supply.PaidTransfer);
        Assert.Equal(0m, supply.PaidCash);
        Assert.Empty(await PaymentsAsync(supplierId));
    }

    [Fact]
    public async Task Attach_ForeignPayment_Throws()
    {
        var (warehouseId, variantId) = await SetupAsync();
        var supplier1 = await CreateSupplierAsync("Ta'minotchi 1");
        var supplier2 = await CreateSupplierAsync("Ta'minotchi 2");
        var supply1 = await CreateSupplyAsync(supplier1, warehouseId, variantId);
        await CreateSupplyAsync(supplier2, warehouseId, variantId);

        await PayAsync(supplier2, 40_000m);
        var foreign = Assert.Single(await PaymentsAsync(supplier2));

        await Assert.ThrowsAsync<BusinessRuleException>(() => AttachAsync(supply1, [foreign.TransactionId]));

        Assert.Equal(0m, (await SupplyAsync(supply1)).PaidTransfer);
        Assert.Single(await PaymentsAsync(supplier2));
    }

    [Fact]
    public async Task Attach_AlreadyAttached_Throws()
    {
        var (warehouseId, variantId) = await SetupAsync();
        var supplierId = await CreateSupplierAsync();
        var supply1 = await CreateSupplyAsync(supplierId, warehouseId, variantId);
        var supply2 = await CreateSupplyAsync(supplierId, warehouseId, variantId);

        await PayAsync(supplierId, 40_000m);
        var payment = Assert.Single(await PaymentsAsync(supplierId));

        await AttachAsync(supply1, [payment.TransactionId]);

        await Assert.ThrowsAsync<BusinessRuleException>(() => AttachAsync(supply2, [payment.TransactionId]));

        Assert.Equal(40_000m, (await SupplyAsync(supply1)).PaidTransfer);
        Assert.Equal(0m, (await SupplyAsync(supply2)).PaidTransfer);
    }
}
