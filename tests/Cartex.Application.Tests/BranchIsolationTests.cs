using Cartex.Application.Tests.Common;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class BranchIsolationTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task Cashier_sees_only_own_branch_sales_admin_sees_all()
    {
        long branch1, branch2, businessId, cashierId;

        fixture.CurrentUser.Reset();
        using (var scope = fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            branch1 = (await db.Branches.FirstAsync(b => b.Name == "Filial 1")).Id;
            branch2 = (await db.Branches.FirstAsync(b => b.Name == "Filial 2")).Id;
            businessId = (await db.Businesses.FirstAsync()).Id;
            cashierId = (await db.Users.FirstAsync(u => u.Username == "cashier")).Id;
        }

        fixture.CurrentUser.AsCashier(cashierId, businessId, branch1);
        using (var scope = fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var branchIds = await db.Sales.Select(s => s.BranchId).Distinct().ToListAsync();
            Assert.NotEmpty(branchIds);
            Assert.All(branchIds, id => Assert.Equal(branch1, id));
            Assert.DoesNotContain(branch2, branchIds);
        }

        fixture.CurrentUser.AsAdmin(1, businessId, branch1, branch2);
        using (var scope = fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var branchIds = await db.Sales.Select(s => s.BranchId).Distinct().ToListAsync();
            Assert.Contains(branch2, branchIds);
        }
    }
}
