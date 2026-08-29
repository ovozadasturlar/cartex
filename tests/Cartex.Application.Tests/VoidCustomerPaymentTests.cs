using Cartex.Application.Common.Messaging;
using Cartex.Application.CustomerPayments.Commands;
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
public class VoidCustomerPaymentTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private async Task<(long BranchId, long WarehouseId, long VariantId)> SetupAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branchId = await db.Branches.Where(x => x.Name == "Asosiy filial").Select(x => x.Id).FirstAsync();
        var warehouseId = await db.Warehouses.Where(x => x.Name == "Asosiy ombor").Select(x => x.Id).FirstAsync();
        var businessId = await db.Businesses.Select(x => x.Id).FirstAsync();
        var adminId = await db.Users.Where(x => x.Username == "admin").Select(x => x.Id).FirstAsync();
        var variantId = await db.ProductVariants
            .Where(x => x.Product.Name == "Smesitel oshxona Zegor")
            .Select(x => x.Id)
            .FirstAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branchId);
        await TestShift.OpenAsync(Fixture);
        return (branchId, warehouseId, variantId);
    }

    private async Task<long> CreateCustomerAsync() =>
        await Send(s => s.Send(new CreateCustomerCommand("Storno Mijoz",
            "+998" + Random.Shared.NextInt64(100_000_000, 999_999_999), null, 0m, CreditLimit: 100_000_000m)));

    private async Task<T> Send<T>(Func<ISender, Task<T>> action)
    {
        using var scope = Fixture.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<ISender>());
    }

    private async Task<decimal> DebtAsync(long customerId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Accounts
            .Where(x => x.CustomerId == customerId && x.Type == AccountType.Debt)
            .SumAsync(x => x.Balance);
    }

    private async Task<(long PaymentId, long CustomerId)> DebtSaleAndPaymentAsync(decimal paid)
    {
        var (branchId, warehouseId, variantId) = await SetupAsync();
        var customerId = await CreateCustomerAsync();
        await Send(s => s.Send(new CreateSaleCommand(warehouseId, customerId, 0, 0, 0, [new CreateSaleItemDto(variantId, 1, 50_000m)])
        {
            UseCustomerAdvance = false
        }));

        var payment = await Send(s => s.Send(new CreateCustomerPaymentCommand(
            customerId, branchId,
            [new CustomerPaymentTenderInput(PaymentMethod.Cash, "uzs", paid)],
            IdempotencyKey: $"void-payment-{Guid.NewGuid():N}")));
        return (payment.Id, customerId);
    }

    [Fact]
    public async Task Void_restores_the_debt_the_payment_cleared()
    {
        var (paymentId, customerId) = await DebtSaleAndPaymentAsync(30_000m);
        var afterPayment = await DebtAsync(customerId);

        await Send(async s =>
        {
            await s.Send(new VoidCustomerPaymentCommand(paymentId, "Xato kiritildi"));
            return 0;
        });

        Assert.Equal(afterPayment + 30_000m, await DebtAsync(customerId));

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var document = await db.CustomerPaymentDocuments.FirstAsync(x => x.Id == paymentId);
        Assert.Equal(BusinessDocumentStatus.Voided, document.Status);
        Assert.Contains("Xato kiritildi", document.Note);
    }

    [Fact]
    public async Task Void_leaves_the_ledger_balanced()
    {
        var (paymentId, _) = await DebtSaleAndPaymentAsync(20_000m);
        await Send(async s =>
        {
            await s.Send(new VoidCustomerPaymentCommand(paymentId, "Storno"));
            return 0;
        });

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var rows = await db.Transactions
            .Where(x => x.CustomerPaymentDocumentId == paymentId)
            .ToListAsync();
        Assert.NotEmpty(rows);
        // every original posting has an exact mirror, so the pair nets to zero per account
        var net = rows.GroupBy(x => x.FromAccountId).Sum(g => g.Sum(x => x.Amount))
                  - rows.GroupBy(x => x.ToAccountId).Sum(g => g.Sum(x => x.Amount));
        Assert.Equal(0m, net);
    }

    [Fact]
    public async Task Voiding_twice_is_rejected()
    {
        var (paymentId, _) = await DebtSaleAndPaymentAsync(10_000m);
        await Send(async s =>
        {
            await s.Send(new VoidCustomerPaymentCommand(paymentId, "Birinchi"));
            return 0;
        });

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Send(async s =>
        {
            await s.Send(new VoidCustomerPaymentCommand(paymentId, "Ikkinchi"));
            return 0;
        }));
        Assert.Equal("payment_not_voidable", error.Code);
    }
}
