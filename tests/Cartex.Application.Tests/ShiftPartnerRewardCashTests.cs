using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Participants;
using Cartex.Application.Customers.Commands;
using Cartex.Application.Partners.Commands;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Shifts.Commands;
using Cartex.Application.Shifts.Queries;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Shared.Models.Shifts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public sealed class ShiftPartnerRewardCashTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    // SMENA-03, SMENA-08: the pay-in is what makes the ledger cash cover the reward.
    private const decimal Float = 100_000m;
    private const decimal CashSale = 300_000m;
    private const decimal PayIn = 200_000m;
    private const decimal Reward = 400_000m;
    private const decimal RewardSale = 4_000_000m;
    private const decimal Expected = 200_000m;

    private sealed record Setup(long BranchId, long WarehouseId, long BusinessId, long AdminId, long RewardVariantId, long CashVariantId);

    private async Task<Setup> StartAsync()
    {
        Setup setup;
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var branchId = await db.Branches.Where(x => x.Name == "Asosiy filial").Select(x => x.Id).FirstAsync();
            var warehouseId = await db.Warehouses.Where(x => x.Name == "Asosiy ombor").Select(x => x.Id).FirstAsync();
            var businessId = await db.Businesses.Select(x => x.Id).FirstAsync();
            var adminId = await db.Users.Where(x => x.Username == "admin").Select(x => x.Id).FirstAsync();
            var variants = await db.Stocks.Where(x => x.WarehouseId == warehouseId && x.Quantity >= 1)
                .Select(x => x.VariantId).Distinct().Take(2).ToListAsync();
            Assert.Equal(2, variants.Count);
            setup = new Setup(branchId, warehouseId, businessId, adminId, variants[0], variants[1]);
        }
        Fixture.CurrentUser.AsAdmin(setup.AdminId, setup.BusinessId, setup.BranchId);
        return setup;
    }

    private async Task<long> AccrueCashRewardAsync(Setup setup)
    {
        long partnerId;
        long roleId;
        long partyId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var customerId = await sender.Send(new CreateCustomerCommand(
                "Usta hamkor", "+998" + Random.Shared.NextInt64(100_000_000, 999_999_999), null, 0m));
            partnerId = await sender.Send(new CreatePartnerCommand("Usta hamkor", CustomerId: customerId));
            roleId = await sender.Send(new SaveParticipantRoleCommand(null, "shift_referrer", "Usta", "Ustalar"));
            await sender.Send(new SavePartnerProgramCommand(null, roleId, "Usta 10%", true,
                PartnerRewardMode.Cash, PartnerRewardBasis.NetRevenue, PartnerRewardTrigger.Sale, 10m));
            partyId = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().PartnerProfiles
                .Where(x => x.Id == partnerId).Select(x => x.PartyId).SingleAsync();
        }

        using (var scope = Fixture.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                new CreateSaleCommand(setup.WarehouseId, null, 0, RewardSale, 0,
                    [new CreateSaleItemDto(setup.RewardVariantId, 1m, RewardSale)])
                {
                    ApplyAutoDiscount = false,
                    Participants = [new ParticipantInput(roleId, partyId)]
                });
        }

        using (var scope = Fixture.CreateScope())
        {
            var earned = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().PartnerRewardEntries
                .Where(x => x.PartnerProfileId == partnerId).SumAsync(x => x.Amount);
            Assert.Equal(Reward, earned);
        }

        return partnerId;
    }

    private async Task<CurrentShiftDto> CurrentAsync()
    {
        using var scope = Fixture.CreateScope();
        var current = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new GetCurrentShiftQuery());
        Assert.NotNull(current);
        return current;
    }

    // SMENA-03, QARZ-10
    [Fact]
    public async Task SMENA_03_cash_partner_reward_lowers_expected_cash_by_its_amount()
    {
        var setup = await StartAsync();
        var partnerId = await AccrueCashRewardAsync(setup);
        await TestShift.OpenAsync(Fixture, Float);

        using (var scope = Fixture.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                new CreateSaleCommand(setup.WarehouseId, null, CashSale, 0, 0,
                    [new CreateSaleItemDto(setup.CashVariantId, 1m, CashSale)])
                {
                    ApplyAutoDiscount = false
                });
        }

        using (var scope = Fixture.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>()
                .Send(new AddCashMovementCommand(PayIn, IsPayOut: false));
        }

        var beforeReward = await CurrentAsync();
        Assert.Equal(Float + CashSale + PayIn, beforeReward.ExpectedCash);

        using (var scope = Fixture.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RedeemPartnerRewardCommand(
                partnerId, PartnerRewardMode.Cash, Reward, setup.BranchId,
                Note: "Naqd mukofot", IdempotencyKey: "smena03-partner-cash"));
        }

        var afterReward = await CurrentAsync();
        Assert.Equal(beforeReward.ExpectedCash - Reward, afterReward.ExpectedCash);
        Assert.Equal(Expected, afterReward.ExpectedCash);
    }

    // SMENA-03
    [Fact]
    public async Task SMENA_03_counted_cash_matching_expected_leaves_no_shortage()
    {
        var setup = await StartAsync();
        var partnerId = await AccrueCashRewardAsync(setup);
        await TestShift.OpenAsync(Fixture, Float);

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new CreateSaleCommand(setup.WarehouseId, null, CashSale, 0, 0,
                [new CreateSaleItemDto(setup.CashVariantId, 1m, CashSale)])
            {
                ApplyAutoDiscount = false
            });
            await sender.Send(new AddCashMovementCommand(PayIn, IsPayOut: false));
            await sender.Send(new RedeemPartnerRewardCommand(
                partnerId, PartnerRewardMode.Cash, Reward, setup.BranchId,
                Note: "Naqd mukofot", IdempotencyKey: "smena03-partner-cash-close"));
        }

        var current = await CurrentAsync();

        ZReportDto report;
        using (var scope = Fixture.CreateScope())
        {
            report = await scope.ServiceProvider.GetRequiredService<ISender>()
                .Send(new CloseShiftCommand(current.Id, current.ExpectedCash));
        }

        Assert.Equal(Expected, report.ExpectedCash);
        Assert.Equal(report.OpeningFloat + report.CashSales - report.CashReturns
            + report.PayIn - report.PayOut + report.DebtPayIn - report.SupplyPayOut, report.ExpectedCash);
        Assert.Equal(0m, report.Difference);
    }
}
