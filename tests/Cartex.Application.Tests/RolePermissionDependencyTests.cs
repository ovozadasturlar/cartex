using Cartex.Application.Roles.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class RolePermissionDependencyTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task Granting_users_manage_also_grants_its_dependencies()
    {
        long businessId, developerId, branch1, sellerRoleId, usersManageId;
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            businessId = (await db.Businesses.FirstAsync()).Id;
            developerId = (await db.Users.FirstAsync(u => u.Username == "developer")).Id;
            branch1 = (await db.Branches.FirstAsync(b => b.Name == "Asosiy filial")).Id;
            sellerRoleId = (await db.Roles.FirstAsync(r => r.Name == "seller")).Id;
            usersManageId = (await db.Permissions.FirstAsync(p => p.Name == "users.manage")).Id;
        }
        Fixture.CurrentUser.AsAdmin(developerId, businessId, branch1);

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new AssignPermissionsCommand(sellerRoleId, [usersManageId]));
        }

        using var check = Fixture.CreateScope();
        var db2 = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var names = await db2.RolePermissions
            .Where(rp => rp.RoleId == sellerRoleId)
            .Select(rp => rp.Permission.Name)
            .ToListAsync();

        Assert.Contains("users.manage", names);
        Assert.Contains("users.view", names);
        Assert.Contains("roles.view", names);
        Assert.Contains("branches.view", names);
    }
}
