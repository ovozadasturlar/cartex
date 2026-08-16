using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Participants;
using Cartex.Application.CustomerReturns.Commands;
using Cartex.Application.CustomerPayments.Commands;
using Cartex.Application.Customers.Commands;
using Cartex.Application.Partners.Commands;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Shared.Models.Partners;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public sealed class PartnerRewardTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task Same_party_can_be_buyer_and_partner_and_returns_reverse_exact_line_reward()
    {
        long branchId;
        long warehouseId;
        long businessId;
        long adminId;
        long eligibleVariantId;
        long excludedVariantId;
        long excludedProductId;
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            branchId = await db.Branches.Where(x => x.Name == "Asosiy filial").Select(x => x.Id).FirstAsync();
            warehouseId = await db.Warehouses.Where(x => x.Name == "Asosiy ombor").Select(x => x.Id).FirstAsync();
            businessId = await db.Businesses.Select(x => x.Id).FirstAsync();
            adminId = await db.Users.Where(x => x.Username == "admin").Select(x => x.Id).FirstAsync();
            var variants = await db.Stocks.Where(x => x.WarehouseId == warehouseId && x.Quantity >= 2)
                .Select(x => new { x.VariantId, x.Variant.ProductId })
                .Distinct().Take(2).ToListAsync();
            Assert.Equal(2, variants.Count);
            eligibleVariantId = variants[0].VariantId;
            excludedVariantId = variants[1].VariantId;
            excludedProductId = variants[1].ProductId;
        }
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branchId);
        await TestShift.OpenAsync(Fixture);

        long customerId;
        long partnerId;
        long roleId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            customerId = await sender.Send(new CreateCustomerCommand(
                "Hamkor xaridor", "+998" + Random.Shared.NextInt64(100_000_000, 999_999_999),
                null, 0, CreditLimit: 100_000_000m));
            partnerId = await sender.Send(new CreatePartnerCommand("Hamkor xaridor", CustomerId: customerId));
            roleId = await sender.Send(new SaveParticipantRoleCommand(null, "referrer", "Usta", "Ustalar"));
            await sender.Send(new SavePartnerProgramCommand(null, roleId, "Usta 10%", true,
                PartnerRewardMode.Cash, PartnerRewardBasis.NetRevenue, PartnerRewardTrigger.Sale, 10m,
                Rules:
                [
                    new PartnerRewardRuleInput(CashbackScope.Product, excludedProductId, IsExcluded: true)
                ]));
        }

        long saleId;
        long eligibleItemId;
        long excludedItemId;
        long partyId;
        decimal earnedBeforeReturn;
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            partyId = await db.PartnerProfiles.Where(x => x.Id == partnerId).Select(x => x.PartyId).SingleAsync();
            var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new CreateSaleCommand(warehouseId, customerId, 150_000m, 0, 0, [
                    new CreateSaleItemDto(eligibleVariantId, 2m, 50_000m),
                    new CreateSaleItemDto(excludedVariantId, 1m, 50_000m)
                ])
            {
                ApplyAutoDiscount = false,
                Participants = [new ParticipantInput(roleId, partyId)]
            });
            saleId = result.SaleId;
            var rows = await db.SaleItems.Where(x => x.SaleId == saleId)
                .Select(x => new { x.Id, x.VariantId }).ToListAsync();
            eligibleItemId = rows.Single(x => x.VariantId == eligibleVariantId).Id;
            excludedItemId = rows.Single(x => x.VariantId == excludedVariantId).Id;
        }

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            earnedBeforeReturn = await db.PartnerRewardEntries.Where(x => x.PartnerProfileId == partnerId)
                .SumAsync(x => x.Amount);
            Assert.True(earnedBeforeReturn > 0);
            var participant = await db.SaleParticipants.SingleAsync(x => x.SaleId == saleId);
            Assert.Equal(partyId, participant.PartyId);
            Assert.Equal(ParticipantAttributionSource.Direct, participant.Source);
        }

        // Returning the excluded line must not reduce a reward earned by another line.
        using (var scope = Fixture.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new CreateCustomerReturnCommand(
                warehouseId,
                [new CustomerReturnLineInput(excludedVariantId, 1m, excludedItemId, null, "Mos kelmadi",
                    ReturnItemCondition.Sellable, InventoryDisposition.SellableRestock)],
                customerId,
                IdempotencyKey: "partner-excluded-return"));
        }
        using (var scope = Fixture.CreateScope())
        {
            var total = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().PartnerRewardEntries
                .Where(x => x.PartnerProfileId == partnerId).SumAsync(x => x.Amount);
            Assert.Equal(earnedBeforeReturn, total);
        }

        PartnerRedemptionCreatedDto redemption;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var command = new RedeemPartnerRewardCommand(partnerId, PartnerRewardMode.Cash, earnedBeforeReturn,
                branchId, Note: "Naqd mukofot", IdempotencyKey: "partner-cash-redemption");
            redemption = await sender.Send(command);
            Assert.Equal(redemption, await sender.Send(command));
        }

        // A return after payout becomes a recoverable negative balance instead of silently
        // changing an unrelated product's reward.
        using (var scope = Fixture.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new CreateCustomerReturnCommand(
                warehouseId,
                [new CustomerReturnLineInput(eligibleVariantId, 1m, eligibleItemId, null, "Ortiqcha",
                    ReturnItemCondition.Sellable, InventoryDisposition.SellableRestock)],
                customerId,
                IdempotencyKey: "partner-eligible-return"));
        }

        using var check = Fixture.CreateScope();
        var db2 = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(-Math.Round(earnedBeforeReturn / 2m, 4), await db2.PartnerRewardEntries
            .Where(x => x.PartnerProfileId == partnerId).SumAsync(x => x.Amount));
        Assert.Equal(1, await db2.PartnerRedemptionDocuments.CountAsync(x => x.Id == redemption.Id));
        Assert.Equal(1, await db2.Transactions.CountAsync(x =>
            x.PartnerRedemptionDocumentId == redemption.Id && x.OperationType == OperationType.PartnerRewardCash));
        Assert.Equal(1, await db2.PartnerRewardEntries.CountAsync(x =>
            x.CustomerReturnDocumentId != null && x.SaleItemId == eligibleItemId
            && x.State == PartnerRewardState.Reversed));
    }

    [Fact]
    public async Task Payment_trigger_accrues_only_as_debt_is_actually_paid()
    {
        long branchId;
        long warehouseId;
        long businessId;
        long adminId;
        long variantId;
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            branchId = await db.Branches.Where(x => x.Name == "Asosiy filial").Select(x => x.Id).FirstAsync();
            warehouseId = await db.Warehouses.Where(x => x.Name == "Asosiy ombor").Select(x => x.Id).FirstAsync();
            businessId = await db.Businesses.Select(x => x.Id).FirstAsync();
            adminId = await db.Users.Where(x => x.Username == "admin").Select(x => x.Id).FirstAsync();
            variantId = await db.Stocks.Where(x => x.WarehouseId == warehouseId && x.Quantity >= 1)
                .Select(x => x.VariantId).FirstAsync();
        }
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branchId);
        await TestShift.OpenAsync(Fixture);

        long customerId;
        long partnerId;
        long roleId;
        long partyId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            customerId = await sender.Send(new CreateCustomerCommand(
                "To'lov hamkori", "+998" + Random.Shared.NextInt64(100_000_000, 999_999_999),
                null, 0, CreditLimit: 100_000_000m));
            partnerId = await sender.Send(new CreatePartnerCommand("To'lov hamkori", CustomerId: customerId));
            roleId = await sender.Send(new SaveParticipantRoleCommand(null, "payment_referrer", "Hamkor", "Hamkorlar"));
            await sender.Send(new SavePartnerProgramCommand(null, roleId, "To'langanda 10%", true,
                PartnerRewardMode.Points, PartnerRewardBasis.NetRevenue, PartnerRewardTrigger.Payment, 10m));
            partyId = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().PartnerProfiles
                .Where(x => x.Id == partnerId).Select(x => x.PartyId).SingleAsync();
        }

        long saleId;
        decimal total;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            saleId = (await sender.Send(new CreateSaleCommand(warehouseId, customerId, 0, 0, 0, [new CreateSaleItemDto(variantId, 1m, 100_000m)])
            {
                ApplyAutoDiscount = false,
                UseCustomerAdvance = false,
                Participants = [new ParticipantInput(roleId, partyId)]
            })).SaleId;
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            total = await db.Sales.Where(x => x.Id == saleId).Select(x => x.TotalAmount).SingleAsync();
            Assert.Equal(0, await db.PartnerRewardEntries.CountAsync(x => x.PartnerProfileId == partnerId));
        }

        var firstHalf = Math.Round(total / 2m, 2);
        using (var scope = Fixture.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new CreateCustomerPaymentCommand(
                customerId, branchId,
                [new CustomerPaymentTenderInput(PaymentMethod.Cash, "UZS", firstHalf)],
                [new CustomerPaymentAllocationInput("UZS", firstHalf, saleId)],
                AutoAllocateDebt: false,
                IdempotencyKey: "partner-payment-half-1"));
        }
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var earned = await db.PartnerRewardEntries.Where(x => x.PartnerProfileId == partnerId)
                .SumAsync(x => x.Amount);
            Assert.Equal(Math.Round(firstHalf * .10m, 4), earned);
            Assert.All(await db.PartnerRewardEntries.Where(x => x.PartnerProfileId == partnerId).ToListAsync(),
                x => Assert.NotNull(x.CustomerPaymentDocumentId));
        }

        var remainder = total - firstHalf;
        using (var scope = Fixture.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new CreateCustomerPaymentCommand(
                customerId, branchId,
                [new CustomerPaymentTenderInput(PaymentMethod.Cash, "UZS", remainder)],
                [new CustomerPaymentAllocationInput("UZS", remainder, saleId)],
                AutoAllocateDebt: false,
                IdempotencyKey: "partner-payment-half-2"));
        }
        using var check = Fixture.CreateScope();
        Assert.Equal(Math.Round(total * .10m, 4), await check.ServiceProvider
            .GetRequiredService<ApplicationDbContext>().PartnerRewardEntries
            .Where(x => x.PartnerProfileId == partnerId).SumAsync(x => x.Amount));
    }
}
