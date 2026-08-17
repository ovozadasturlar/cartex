using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Models;
using Cartex.Application.Shifts.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common.Exceptions;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Cartex.Shared.Models.Common;

namespace Cartex.Application.Tests;

[Collection("database")]
public class ShiftCurrencyValidationTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private async Task<(long branch1, long businessId, long adminId)> SetupAsync(bool multicurrency)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Features.Where(f => f.Code == FeatureCatalog.Multicurrency
                                     || f.Code == FeatureCatalog.PricingMulticurrency
                                     || f.Code == FeatureCatalog.SalesMulticurrency)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.IsEnabled, multicurrency));
        var branch1 = (await db.Branches.FirstAsync(b => b.Name == "Asosiy filial")).Id;
        var businessId = (await db.Businesses.FirstAsync()).Id;
        var adminId = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;
        return (branch1, businessId, adminId);
    }

    [Fact]
    public async Task Opening_shift_with_currency_rows_rejected_when_multicurrency_disabled()
    {
        var (branch1, businessId, adminId) = await SetupAsync(multicurrency: false);
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);

        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            sender.Send(new OpenShiftCommand(0, [new CurrencyAmountDto("USD", 10m)])));
    }

    [Fact]
    public async Task Opening_shift_with_unknown_currency_rejected_when_multicurrency_enabled()
    {
        var (branch1, businessId, adminId) = await SetupAsync(multicurrency: true);
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);

        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            sender.Send(new OpenShiftCommand(0, [new CurrencyAmountDto("XXX", 10m)])));
    }

    [Fact]
    public async Task Closing_shift_with_new_currency_row_rejected_when_multicurrency_disabled()
    {
        var (branch1, businessId, adminId) = await SetupAsync(multicurrency: false);
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        var shiftId = await TestShift.OpenAsync(Fixture);

        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            sender.Send(new CloseShiftCommand(shiftId, 0, [new CurrencyAmountDto("USD", 5m)])));
    }

    [Fact]
    public async Task Base_currency_row_rejected_on_open()
    {
        var (branch1, businessId, adminId) = await SetupAsync(multicurrency: true);
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var baseCode = (await db.Businesses.FirstAsync()).Currency;
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            sender.Send(new OpenShiftCommand(0, [new CurrencyAmountDto(baseCode, 10m)])));
    }
}
