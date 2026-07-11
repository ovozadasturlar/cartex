using Cartex.Application.Tests.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class BranchIsolationTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task Cashier_sees_only_own_branch_sales_admin_sees_all()
    {
        long branch1, branch2, businessId, cashierId, adminId, wh1, wh2;

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            branch1 = (await db.Branches.FirstAsync(b => b.Name == "Filial 1")).Id;
            branch2 = (await db.Branches.FirstAsync(b => b.Name == "Filial 2")).Id;
            businessId = (await db.Businesses.FirstAsync()).Id;
            cashierId = (await db.Users.FirstAsync(u => u.Username == "seller")).Id;
            adminId = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;
            wh1 = (await db.Warehouses.FirstAsync(w => w.BranchId == branch1)).Id;
            wh2 = (await db.Warehouses.FirstAsync(w => w.BranchId == branch2)).Id;
        }

        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1, branch2);
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Sales.Add(new Sale { BranchId = branch1, WarehouseId = wh1, UserId = adminId, Status = SaleStatus.Completed, ReceiptToken = Guid.NewGuid().ToString("N") });
            db.Sales.Add(new Sale { BranchId = branch2, WarehouseId = wh2, UserId = adminId, Status = SaleStatus.Completed, ReceiptToken = Guid.NewGuid().ToString("N") });
            await db.SaveChangesAsync();
        }

        Fixture.CurrentUser.AsCashier(cashierId, businessId, branch1);
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var branchIds = await db.Sales.Select(s => s.BranchId).Distinct().ToListAsync();
            Assert.NotEmpty(branchIds);
            Assert.All(branchIds, id => Assert.Equal(branch1, id));
            Assert.DoesNotContain(branch2, branchIds);
        }

        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1, branch2);
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var branchIds = await db.Sales.Select(s => s.BranchId).Distinct().ToListAsync();
            Assert.Contains(branch2, branchIds);
        }
    }
}
