using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Participants;
using Cartex.Application.Customers.Commands;
using Cartex.Application.Ordering.Commands;
using Cartex.Application.Partners.Commands;
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
public sealed class OrderingCheckoutDraftTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task Queue_preserves_foreign_payment_and_partner_until_checkout()
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
            variantId = await db.Stocks.Where(x => x.WarehouseId == warehouseId && x.Quantity > 0)
                .Select(x => x.VariantId).FirstAsync();
        }
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branchId);
        await TestShift.OpenAsync(Fixture);

        long customerId;
        long partyId;
        long roleId;
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Features.Where(x => x.Code == FeatureCatalog.Multicurrency
                                          || x.Code == FeatureCatalog.PricingMulticurrency
                                          || x.Code == FeatureCatalog.SalesMulticurrency)
                .ExecuteUpdateAsync(x => x.SetProperty(f => f.IsEnabled, true));
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new SetExchangeRateCommand("USD", 12_500m));
            customerId = await sender.Send(new CreateCustomerCommand(
                "Navbat mijozi", "+998" + Random.Shared.NextInt64(100_000_000, 999_999_999),
                null, 0, CreditLimit: 100_000_000m));
            var partnerId = await sender.Send(new CreatePartnerCommand("Navbat hamkori",
                Phone: "+998" + Random.Shared.NextInt64(100_000_000, 999_999_999)));
            partyId = await db.PartnerProfiles.Where(x => x.Id == partnerId).Select(x => x.PartyId).SingleAsync();
            roleId = await sender.Send(new SaveParticipantRoleCommand(null, "queue_partner", "Hamkor", "Hamkorlar"));
        }

        string code;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            code = await sender.Send(new SubmitCartCommand(warehouseId, customerId, [new SubmitCartItemDto(variantId, 1m)])
            {
                IdempotencyKey = "queue-payment-partner-submit",
                Participants = [new ParticipantInput(roleId, partyId)],
                Payments = [new SalePaymentDto(PaymentMethod.Cash, "usd", 1m)],
                DebtCurrency = "uzs",
                UseCustomerAdvance = false
            });
        }

        long saleId;
        using (var scope = Fixture.CreateScope())
        {
            saleId = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new CheckoutCartCommand(code, 0, 0, 0)
            {
                IdempotencyKey = "queue-payment-partner-checkout"
            });
        }

        using var check = Fixture.CreateScope();
        var db2 = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var payment = await db2.SalePayments.SingleAsync(x => x.SaleId == saleId);
        Assert.Equal(PaymentMethod.Cash, payment.Method);
        Assert.Equal("USD", payment.Currency);
        Assert.Equal(1m, payment.Amount);
        Assert.Equal(12_500m, payment.AmountBase);
        var participant = await db2.SaleParticipants.SingleAsync(x => x.SaleId == saleId);
        Assert.Equal(partyId, participant.PartyId);
        Assert.Equal(ParticipantAttributionSource.CartInherited, participant.Source);
        var cart = await db2.Carts.Include(x => x.Payments).SingleAsync(x => x.AggregateCode == code);
        Assert.Single(cart.Payments);
        Assert.False(cart.UseCustomerAdvance);
    }
}
