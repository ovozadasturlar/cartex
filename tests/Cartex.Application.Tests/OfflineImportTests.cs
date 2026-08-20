using Cartex.Application.Customers.Commands;
using Cartex.Application.Products.Commands;
using Cartex.Application.Supplies.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Cartex.Shared.Models.Customers;
using Cartex.Shared.Models.OfflineCache;
using Cartex.Shared.Models.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class OfflineImportTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private const decimal Price = 10_000m;
    private const string RescueDevice = "rescue-device";

    private static readonly string[] CashierPermissions =
        ["sales.create", "sales.checkout", "sales.view", "customers.view", "rates.view"];

    private sealed record Ctx(long Branch, long Warehouse, long Business, long AdminId, long VariantId, OfflineLeaseGrantDto Grant);

    private async Task<(long Branch, long Warehouse, long Business, long AdminId, long UnitId)> BareSetupAsync()
    {
        long branch, warehouse, business, admin, unit;
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            branch = (await db.Branches.FirstAsync(b => b.Name == "Asosiy filial")).Id;
            warehouse = (await db.Warehouses.FirstAsync(w => w.Name == "Asosiy ombor")).Id;
            business = (await db.Businesses.FirstAsync()).Id;
            admin = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;
            unit = (await db.Units.FirstAsync(u => u.ShortName == "dona")).Id;
        }

        Fixture.CurrentUser.AsAdmin(admin, business, branch);
        return (branch, warehouse, business, admin, unit);
    }

    private async Task<Ctx> SetupAsync()
    {
        var (branch, warehouse, business, admin, unit) = await BareSetupAsync();
        await TestShift.OpenAsync(Fixture);

        long variantId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var productId = await sender.Send(new CreateProductCommand("Oflayn import mahsulot", null, unit, 0, null));
            variantId = (await db.ProductVariants.FirstAsync(v => v.ProductId == productId)).Id;
            await sender.Send(new CreateSupplyCommand(null, warehouse, DateOnly.FromDateTime(DateTime.Today),
                [new CreateSupplyItemDto(variantId, 100, 6_000m, null, SellingPrice: Price)]));
        }

        var grant = await TestOffline.ClaimAsync(Fixture, warehouse);
        return new Ctx(branch, warehouse, business, admin, variantId, grant);
    }

    private static OfflineSyncEventRequest CashSale(long sequence, Ctx ctx) =>
        TestOffline.Event(sequence, "sale.create", new CreateSaleRequest(ctx.Warehouse, null, Price, 0, 0,
            [new CreateSaleItemRequest(ctx.VariantId, 1, Price)]) { ApplyAutoDiscount = false });

    private static OfflineSyncEventRequest BadDebtSale(long sequence, Ctx ctx) =>
        TestOffline.Event(sequence, "sale.create", new CreateSaleRequest(ctx.Warehouse, null, 0, 0, 0,
            [new CreateSaleItemRequest(ctx.VariantId, 1, Price)]) { ApplyAutoDiscount = false });

    private static OfflineSyncEventRequest Payment(long sequence, long customerId, long branchId, decimal amount,
        long? actorUserId = null, string method = "Cash") =>
        TestOffline.Event(sequence, "customer.payment.create", new CreateCustomerPaymentRequest(customerId, branchId,
            [new CustomerPaymentTenderRequest(method, "UZS", amount)], null, true), actorUserId);

    private async Task<long> CreateDebtorAsync(decimal openingDebt)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(new CreateCustomerCommand("Import Qarzdor",
            "+998" + Random.Shared.NextInt64(100_000_000, 999_999_999), null, 0m,
            CreditLimit: 10_000_000m, OpeningBalance: openingDebt));
    }

    private async Task<decimal> DebtAsync(long customerId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.Accounts.FirstOrDefaultAsync(a =>
            a.CustomerId == customerId && a.Type == AccountType.Debt))?.Balance ?? 0m;
    }

    private async Task<int> SalesCountAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Sales.CountAsync();
    }

    [Fact]
    public async Task OFF_41_Import_from_a_different_device_applies_events()
    {
        var ctx = await SetupAsync();
        var before = await SalesCountAsync();
        Fixture.CurrentUser.DeviceId = RescueDevice;

        var result = await TestOffline.ImportAsync(Fixture, ctx.Grant, false, CashSale(1, ctx), CashSale(2, ctx));

        Assert.All(result.Results, r => Assert.Equal("Applied", r.Status));
        Assert.Equal(before + 2, await SalesCountAsync());
        Assert.Equal(2, await TestOffline.LastAcceptedSequenceAsync(Fixture, ctx.Grant.LeaseId));
    }

    [Fact]
    public async Task OFF_41_Import_with_wrong_lease_token_is_refused()
    {
        var ctx = await SetupAsync();
        var before = await SalesCountAsync();
        Fixture.CurrentUser.DeviceId = RescueDevice;

        var error = await Assert.ThrowsAnyAsync<DomainException>(() =>
            TestOffline.ImportAsync(Fixture, ctx.Grant.LeaseId, ctx.Grant.Epoch, "wrong-token", false, CashSale(1, ctx)));

        Assert.Equal("offline_lease_token_invalid", error.Code);
        Assert.Equal(before, await SalesCountAsync());
    }

    [Fact]
    public async Task OFF_41_Import_with_wrong_epoch_is_refused()
    {
        var ctx = await SetupAsync();
        var before = await SalesCountAsync();
        Fixture.CurrentUser.DeviceId = RescueDevice;

        await Assert.ThrowsAnyAsync<DomainException>(() => TestOffline.ImportAsync(Fixture,
            ctx.Grant.LeaseId, ctx.Grant.Epoch + 1, ctx.Grant.LeaseToken, false, CashSale(1, ctx)));

        Assert.Equal(before, await SalesCountAsync());
    }

    [Fact]
    public async Task OFF_41_Import_works_on_a_revoked_lease()
    {
        var ctx = await SetupAsync();
        var before = await SalesCountAsync();
        await TestOffline.ForceReleaseAsync(Fixture, ctx.Grant.LeaseId, "qurilma buzildi");
        Fixture.CurrentUser.DeviceId = RescueDevice;

        var result = await TestOffline.ImportAsync(Fixture, ctx.Grant, false, CashSale(1, ctx), CashSale(2, ctx));

        Assert.All(result.Results, r => Assert.Equal("Applied", r.Status));
        Assert.Equal(before + 2, await SalesCountAsync());
    }

    [Fact]
    public async Task OFF_41_Import_requires_devices_revoke_permission_on_the_caller()
    {
        var ctx = await SetupAsync();
        var before = await SalesCountAsync();
        Fixture.CurrentUser.AsCashier(ctx.AdminId, ctx.Business, ctx.Branch);
        Fixture.CurrentUser.Granted.UnionWith(CashierPermissions);
        Fixture.CurrentUser.DeviceId = RescueDevice;

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            TestOffline.ImportAsync(Fixture, ctx.Grant, false, CashSale(1, ctx)));

        Assert.Equal(before, await SalesCountAsync());
    }

    [Fact]
    public async Task OFF_41_Caller_with_devices_revoke_can_import()
    {
        var ctx = await SetupAsync();
        var before = await SalesCountAsync();
        Fixture.CurrentUser.AsCashier(ctx.AdminId, ctx.Business, ctx.Branch);
        Fixture.CurrentUser.Granted.UnionWith(CashierPermissions);
        Fixture.CurrentUser.Granted.Add("devices.revoke");
        Fixture.CurrentUser.DeviceId = RescueDevice;

        var result = await TestOffline.ImportAsync(Fixture, ctx.Grant, false, CashSale(1, ctx));

        Assert.Equal("Applied", Assert.Single(result.Results).Status);
        Assert.Equal(before + 1, await SalesCountAsync());
    }

    [Fact]
    public async Task OFF_41_Import_reauthorizes_the_event_actor()
    {
        var ctx = await SetupAsync();
        var customerId = await CreateDebtorAsync(50_000m);
        var actorId = await TestOffline.CreateActorAsync(Fixture, ctx.Branch);
        Fixture.CurrentUser.DeviceId = RescueDevice;

        var result = await TestOffline.ImportAsync(Fixture, ctx.Grant, false,
            Payment(1, customerId, ctx.Branch, 50_000m, actorUserId: actorId, method: "Card"));

        Assert.Equal("Rejected", Assert.Single(result.Results).Status);
        Assert.Equal(50_000m, await DebtAsync(customerId));
    }

    [Fact]
    public async Task OFF_43_Rejected_event_stops_the_chain_without_auto_skip()
    {
        var ctx = await SetupAsync();
        var before = await SalesCountAsync();
        Fixture.CurrentUser.DeviceId = RescueDevice;

        var result = await TestOffline.ImportAsync(Fixture, ctx.Grant, false,
            CashSale(1, ctx), BadDebtSale(2, ctx), CashSale(3, ctx));

        Assert.Equal("Applied", result.Results[0].Status);
        Assert.Equal("Rejected", result.Results[1].Status);
        Assert.Equal("Deferred", result.Results[2].Status);
        Assert.Equal("prior_event_rejected", result.Results[2].ErrorCode);
        Assert.Equal(1, await TestOffline.LastAcceptedSequenceAsync(Fixture, ctx.Grant.LeaseId));
        Assert.Equal(before + 1, await SalesCountAsync());
    }

    [Fact]
    public async Task OFF_43_Auto_skip_records_rejected_event_as_skipped_and_continues()
    {
        var ctx = await SetupAsync();
        var before = await SalesCountAsync();
        Fixture.CurrentUser.DeviceId = RescueDevice;

        var result = await TestOffline.ImportAsync(Fixture, ctx.Grant, skipRejected: true,
            CashSale(1, ctx), BadDebtSale(2, ctx), CashSale(3, ctx));

        Assert.Equal("Applied", result.Results[0].Status);
        Assert.Equal("Skipped", result.Results[1].Status);
        Assert.Equal("Applied", result.Results[2].Status);
        Assert.Equal(3, await TestOffline.LastAcceptedSequenceAsync(Fixture, ctx.Grant.LeaseId));
        Assert.Equal(before + 2, await SalesCountAsync());
    }

    [Fact]
    public async Task OFF_42_Reimporting_the_same_file_is_idempotent()
    {
        var ctx = await SetupAsync();
        var before = await SalesCountAsync();
        var events = new[] { CashSale(1, ctx), CashSale(2, ctx), CashSale(3, ctx) };
        Fixture.CurrentUser.DeviceId = RescueDevice;

        var first = await TestOffline.ImportAsync(Fixture, ctx.Grant, false, events);
        Assert.All(first.Results, r => Assert.Equal("Applied", r.Status));

        var second = await TestOffline.ImportAsync(Fixture, ctx.Grant, false, events);

        Assert.All(second.Results, r => Assert.Equal("AlreadyApplied", r.Status));
        Assert.Equal(first.Results.Select(r => r.ResultEntityId), second.Results.Select(r => r.ResultEntityId));
        Assert.Equal(before + 3, await SalesCountAsync());
        Assert.Equal(3, await TestOffline.LastAcceptedSequenceAsync(Fixture, ctx.Grant.LeaseId));
    }

    [Fact]
    public async Task OFF_42_Original_device_push_after_import_returns_already_applied()
    {
        var ctx = await SetupAsync();
        var before = await SalesCountAsync();
        var events = new[] { CashSale(1, ctx), CashSale(2, ctx), CashSale(3, ctx) };
        Fixture.CurrentUser.DeviceId = RescueDevice;

        var import = await TestOffline.ImportAsync(Fixture, ctx.Grant, false, events);
        Assert.All(import.Results, r => Assert.Equal("Applied", r.Status));

        Fixture.CurrentUser.DeviceId = TestOffline.Device;
        var push = await TestOffline.PushAsync(Fixture, ctx.Grant, events);

        Assert.All(push.Results, r => Assert.Equal("AlreadyApplied", r.Status));
        Assert.Equal(before + 3, await SalesCountAsync());
        Assert.Equal(3, await TestOffline.LastAcceptedSequenceAsync(Fixture, ctx.Grant.LeaseId));
    }

    [Fact]
    public async Task OFF_43_Reimport_after_cause_removed_applies_remaining_events()
    {
        var (branch, warehouse, _, _, _) = await BareSetupAsync();
        var customerId = await CreateDebtorAsync(90_000m);
        var grant = await TestOffline.ClaimAsync(Fixture, warehouse);
        var events = new[]
        {
            Payment(1, customerId, branch, 30_000m),
            Payment(2, customerId, branch, 30_000m),
            Payment(3, customerId, branch, 30_000m)
        };
        Fixture.CurrentUser.DeviceId = RescueDevice;

        var blocked = await TestOffline.ImportAsync(Fixture, grant, false, events);
        Assert.Equal("Rejected", blocked.Results[0].Status);
        Assert.Equal("Deferred", blocked.Results[1].Status);
        Assert.Equal("Deferred", blocked.Results[2].Status);
        Assert.Equal(90_000m, await DebtAsync(customerId));

        await TestShift.OpenAsync(Fixture);
        var retried = await TestOffline.ImportAsync(Fixture, grant, false, events);

        Assert.All(retried.Results, r => Assert.Equal("Applied", r.Status));
        Assert.Equal(0m, await DebtAsync(customerId));
        Assert.Equal(3, await TestOffline.LastAcceptedSequenceAsync(Fixture, grant.LeaseId));
    }
}
