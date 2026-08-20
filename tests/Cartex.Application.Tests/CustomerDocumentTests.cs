using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Application.CustomerPayments.Commands;
using Cartex.Application.CustomerReturns.Commands;
using Cartex.Application.Customers.Commands;
using Cartex.Application.Customers.Queries;
using Cartex.Application.Rates.Commands;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Authorization;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public sealed class CustomerDocumentTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private async Task<(long BranchId, long WarehouseId, long BusinessId, long AdminId, long VariantId)> SetupAsync()
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
        return (branchId, warehouseId, businessId, adminId, variantId);
    }

    private async Task AllowCustomerCreditAsync()
    {
        using var scope = Fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISettingsService>()
            .SetAsync(SettingKeys.SalesPolicy, new SalesPolicySettings { AllowCustomerCredit = true });
    }

    private async Task<long> CreateCustomerAsync(string prefix = "Hujjat Mijoz")
    {
        using var scope = Fixture.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new CreateCustomerCommand(prefix, "+998" + Random.Shared.NextInt64(100_000_000, 999_999_999),
                null, 0m, CreditLimit: 100_000_000m));
    }

    [Fact]
    public async Task Payment_is_immutable_idempotent_and_keeps_overpayment_as_advance()
    {
        var (branchId, warehouseId, _, _, variantId) = await SetupAsync();
        // QARZ-20: ortiqcha to'lov avansda qolishi uchun haqdorlik yoniq bo'lishi shart
        await AllowCustomerCreditAsync();
        var customerId = await CreateCustomerAsync();

        long saleId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            saleId = (await sender.Send(new CreateSaleCommand(warehouseId, customerId, 0, 0, 0, [new CreateSaleItemDto(variantId, 1, 50_000m)])
            {
                UseCustomerAdvance = false
            })).SaleId;
        }

        var command = new CreateCustomerPaymentCommand(
            customerId,
            branchId,
            [new CustomerPaymentTenderInput(PaymentMethod.Cash, "uzs", 70_000m)],
            IdempotencyKey: "payment-doc-retry-1");

        long documentId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var first = await sender.Send(command);
            var retry = await sender.Send(command);
            documentId = first.Id;
            Assert.Equal(first, retry);
            Assert.Equal(50_000m, first.AllocatedBaseAmount);
            Assert.Equal(20_000m, first.AdvanceBaseAmount);
        }

        using var check = Fixture.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await db.CustomerPaymentDocuments.CountAsync(x => x.Id == documentId));
        Assert.Equal(1, await db.CustomerPaymentTenders.CountAsync(x => x.CustomerPaymentDocumentId == documentId));
        Assert.Equal(1, await db.CustomerPaymentAllocations.CountAsync(x => x.CustomerPaymentDocumentId == documentId));
        Assert.Equal(saleId, await db.CustomerPaymentAllocations
            .Where(x => x.CustomerPaymentDocumentId == documentId)
            .Select(x => x.SaleId)
            .SingleAsync());
        Assert.Equal(0m, await db.Accounts
            .Where(x => x.CustomerId == customerId && x.Type == AccountType.Debt)
            .Select(x => x.Balance)
            .SingleAsync());
        Assert.Equal(20_000m, await db.Accounts
            .Where(x => x.CustomerId == customerId && x.Type == AccountType.CustomerAdvance)
            .Select(x => x.Balance)
            .SingleAsync());
        Assert.Equal(3, await db.Transactions.CountAsync(x => x.CustomerPaymentDocumentId == documentId));
    }

    [Fact]
    public async Task Base_tender_can_be_allocated_to_foreign_currency_sale_debt()
    {
        var (branchId, warehouseId, _, _, variantId) = await SetupAsync();
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Features
                .Where(x => x.Code == FeatureCatalog.Multicurrency
                            || x.Code == FeatureCatalog.PricingMulticurrency
                            || x.Code == FeatureCatalog.SalesMulticurrency)
                .ExecuteUpdateAsync(x => x.SetProperty(f => f.IsEnabled, true));
            await scope.ServiceProvider.GetRequiredService<ISender>()
                .Send(new SetExchangeRateCommand("USD", 12_500m));
        }

        var customerId = await CreateCustomerAsync("USD hujjat");
        long saleId;
        using (var scope = Fixture.CreateScope())
        {
            saleId = (await scope.ServiceProvider.GetRequiredService<ISender>().Send(new CreateSaleCommand(warehouseId, customerId, 0, 0, 0, [new CreateSaleItemDto(variantId, 1, 50_000m)])
            {
                DebtCurrency = "usd",
                UseCustomerAdvance = false
            })).SaleId;
        }

        using (var scope = Fixture.CreateScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                new CreateCustomerPaymentCommand(
                    customerId,
                    branchId,
                    [new CustomerPaymentTenderInput(PaymentMethod.Bank, "UZS", 50_000m)],
                    [new CustomerPaymentAllocationInput("usd", 4m, saleId)],
                    AutoAllocateDebt: false,
                    IdempotencyKey: "foreign-allocation-1"));
            Assert.Equal(50_000m, result.AllocatedBaseAmount);
            Assert.Equal(0m, result.AdvanceBaseAmount);
        }

        using var check = Fixture.CreateScope();
        var db2 = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(0m, await db2.Accounts
            .Where(x => x.CustomerId == customerId && x.Type == AccountType.Debt && x.Currency == "USD")
            .Select(x => x.Balance)
            .SingleAsync());
        Assert.Equal(50_000m, await db2.Accounts
            .Where(x => x.BranchId == branchId && x.Type == AccountType.Bank && x.Currency == "UZS")
            .Select(x => x.Balance)
            .SingleAsync());
    }

    [Fact]
    public async Task Return_persists_reason_quarantine_movement_split_settlement_and_is_idempotent()
    {
        var (branchId, warehouseId, _, _, variantId) = await SetupAsync();
        var customerId = await CreateCustomerAsync("Qaytaruv hujjati");
        long saleId;
        long saleItemId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            saleId = (await sender.Send(new CreateSaleCommand(
                warehouseId, customerId, 50_000m, 0, 0,
                [new CreateSaleItemDto(variantId, 1, 50_000m)]))).SaleId;
            saleItemId = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().SaleItems
                .Where(x => x.SaleId == saleId).Select(x => x.Id).SingleAsync();
        }

        var command = new CreateCustomerReturnCommand(
            warehouseId,
            [new CustomerReturnLineInput(
                variantId, 1m, saleItemId, null, "Qobig'i shikastlangan",
                ReturnItemCondition.Damaged, InventoryDisposition.Quarantine)],
            customerId,
            [
                new CustomerReturnSettlementInput(ReturnSettlementMethod.Cash, "UZS", 30_000m),
                new CustomerReturnSettlementInput(ReturnSettlementMethod.CustomerAdvance, "UZS", 20_000m)
            ],
            AutoSettle: false,
            Note: "Mijoz bilan kelishildi",
            IdempotencyKey: "return-doc-retry-1");

        long documentId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var first = await sender.Send(command);
            var retry = await sender.Send(command);
            documentId = first.Id;
            Assert.Equal(first, retry);
        }

        using var check = Fixture.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var document = await db.CustomerReturnDocuments
            .Include(x => x.Lines)
            .Include(x => x.Settlements)
            .SingleAsync(x => x.Id == documentId);
        var line = Assert.Single(document.Lines);
        Assert.Equal("Qobig'i shikastlangan", line.Reason);
        Assert.Equal(ReturnItemCondition.Damaged, line.Condition);
        Assert.Equal(InventoryDisposition.Quarantine, line.Disposition);
        Assert.Equal(2, document.Settlements.Count);
        Assert.Equal(50_000m, document.Settlements.Sum(x => x.AmountBase));
        Assert.Equal(1m, await db.InventoryPositions
            .Where(x => x.BranchId == branchId
                        && x.LocationKind == InventoryLocationKind.Quarantine
                        && x.LocationId == warehouseId
                        && x.VariantId == variantId)
            .Select(x => x.Quantity)
            .SingleAsync());
        var movement = await db.InventoryMovements.SingleAsync(x =>
            x.SourceType == "CustomerReturn" && x.SourceId == documentId);
        Assert.Equal(InventoryLocationKind.Quarantine, movement.ToLocationKind);
        Assert.Equal(20_000m, await db.Accounts
            .Where(x => x.CustomerId == customerId && x.Type == AccountType.CustomerAdvance)
            .Select(x => x.Balance)
            .SingleAsync());
        Assert.Equal(SaleStatus.Returned, await db.Sales.Where(x => x.Id == saleId).Select(x => x.Status).SingleAsync());
        Assert.Equal(1, await db.CustomerReturnDocuments.CountAsync(x => x.Id == documentId));
    }

    [Fact]
    public async Task Customer_credit_refund_is_immutable_and_statement_reconciles_by_currency()
    {
        var (branchId, _, _, _, _) = await SetupAsync();
        // QARZ-20: qarzsiz mijozning to'lovi butunlay avansga tushadi
        await AllowCustomerCreditAsync();
        var customerId = await CreateCustomerAsync("Avans qaytaruv");

        using (var scope = Fixture.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                new CreateCustomerPaymentCommand(
                    customerId,
                    branchId,
                    [new CustomerPaymentTenderInput(PaymentMethod.Cash, "UZS", 30_000m)],
                    AutoAllocateDebt: true,
                    IdempotencyKey: "refund-source-payment"));
        }

        var command = new CreateCustomerRefundCommand(
            customerId,
            branchId,
            [new CustomerRefundTenderInput(PaymentMethod.Cash, "UZS", 10_000m)],
            Note: "Mijoz talabiga ko'ra",
            IdempotencyKey: "customer-refund-retry-1");

        long refundId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var first = await sender.Send(command);
            var retry = await sender.Send(command);
            Assert.Equal(first, retry);
            refundId = first.Id;
        }

        using var check = Fixture.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await db.CustomerRefundDocuments.CountAsync(x => x.Id == refundId));
        Assert.Equal(1, await db.CustomerRefundTenders.CountAsync(x => x.CustomerRefundDocumentId == refundId));
        Assert.Equal(2, await db.Transactions.CountAsync(x => x.CustomerRefundDocumentId == refundId));
        Assert.Equal(20_000m, await db.Accounts
            .Where(x => x.CustomerId == customerId && x.Type == AccountType.CustomerAdvance && x.Currency == "UZS")
            .Select(x => x.Balance).SingleAsync());
        Assert.Equal(20_000m, await db.Accounts
            .Where(x => x.BranchId == branchId && x.Type == AccountType.Cash && x.Currency == "UZS")
            .Select(x => x.Balance).SingleAsync());

        var statement = await check.ServiceProvider.GetRequiredService<ISender>()
            .Send(new GetCustomerStatementQuery(customerId));
        var balance = Assert.Single(statement.Balances, x => x.Currency == "UZS");
        Assert.Equal(-20_000m, balance.ClosingBalance);
        Assert.Contains(statement.Timeline, x => x.Type == "CustomerPayment" && x.Credit == 30_000m);
        Assert.Contains(statement.Timeline, x => x.Type == "CustomerRefund" && x.Debit == 10_000m);
    }
}
