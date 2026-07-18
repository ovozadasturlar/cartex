using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Application.Supplies.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Common.Exceptions;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class SupplyRequireSupplierTests(DatabaseFixture fixture) : DatabaseTest(fixture)
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

    private async Task SetRequireSupplierAsync(bool required)
    {
        using var scope = Fixture.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        await settings.SetAsync(SettingKeys.SalesPolicy, new SalesPolicySettings { RequireSupplier = required });
    }

    [Fact]
    public async Task Supply_without_supplier_allowed_by_default()
    {
        var (branch1, warehouse1, businessId, adminId, variantId) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await SetRequireSupplierAsync(false);

        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var supplyId = await sender.Send(new CreateSupplyCommand(null, warehouse1, DateOnly.FromDateTime(DateTime.Today),
            [new CreateSupplyItemDto(variantId, 3, 8000m, null)]));

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Null((await db.Supplies.FirstAsync(s => s.Id == supplyId)).SupplierId);
    }

    [Fact]
    public async Task Supply_without_supplier_rejected_when_required()
    {
        var (branch1, warehouse1, businessId, adminId, variantId) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await SetRequireSupplierAsync(true);

        try
        {
            using var scope = Fixture.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                sender.Send(new CreateSupplyCommand(null, warehouse1, DateOnly.FromDateTime(DateTime.Today),
                    [new CreateSupplyItemDto(variantId, 3, 8000m, null)])));
        }
        finally
        {
            await SetRequireSupplierAsync(false);
        }
    }
}
