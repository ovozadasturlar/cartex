using Cartex.Application.Sales.Commands;
using Cartex.Application.Shifts;
using Cartex.Application.Shifts.Commands;
using Cartex.Application.Shifts.Queries;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Common.Exceptions;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class ShiftTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private async Task<(long branch1, long warehouse1, long businessId, long adminId, long variantId)> SetupAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branch1 = (await db.Branches.FirstAsync(b => b.Name == "Filial 1")).Id;
        var warehouse1 = (await db.Warehouses.FirstAsync(w => w.Name == "Filial 1 ombori")).Id;
        var businessId = (await db.Businesses.FirstAsync()).Id;
        var adminId = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;
        var productId = (await db.Products.FirstAsync(p => p.Name == "Smesitel oshxona Zegor")).Id;
        var variantId = (await db.ProductVariants.FirstAsync(v => v.ProductId == productId)).Id;
        return (branch1, warehouse1, businessId, adminId, variantId);
    }

    [Fact]
    public async Task ZReport_sums_float_sales_payins_payouts_and_difference()
    {
        var (branch1, warehouse1, businessId, adminId, variantId) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);

        ZReportDto report;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new OpenShiftCommand(100000));
            await sender.Send(new CreateSaleCommand(warehouse1, null, 770000, 0, 0, [new CreateSaleItemDto(variantId, 2)]));
            await sender.Send(new AddCashMovementCommand(50000, IsPayOut: false));
            await sender.Send(new AddCashMovementCommand(30000, IsPayOut: true));

            var current = await sender.Send(new GetCurrentShiftQuery());
            Assert.NotNull(current);
            report = await sender.Send(new CloseShiftCommand(current!.Id, current.ExpectedCash));
        }

        Assert.Equal(100000, report.OpeningFloat);
        Assert.Equal(50000, report.PayIn);
        Assert.Equal(30000, report.PayOut);
        Assert.True(report.CashSales > 0);
        Assert.Equal(report.OpeningFloat + report.CashSales - report.CashReturns + report.PayIn - report.PayOut, report.ExpectedCash);
        Assert.Equal(0, report.Difference);
    }

    [Fact]
    public async Task Close_with_counted_below_expected_reports_negative_difference()
    {
        var (branch1, warehouse1, businessId, adminId, _) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);

        ZReportDto report;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var shiftId = await sender.Send(new OpenShiftCommand(100000));
            report = await sender.Send(new CloseShiftCommand(shiftId, 90000));
        }

        Assert.Equal(100000, report.ExpectedCash);
        Assert.Equal(-10000, report.Difference);
    }

    [Fact]
    public async Task Opening_second_shift_throws()
    {
        var (branch1, _, businessId, adminId, _) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);

        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(new OpenShiftCommand(0));
        await Assert.ThrowsAsync<BusinessRuleException>(() => sender.Send(new OpenShiftCommand(0)));
    }
}
