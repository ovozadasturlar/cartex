using Cartex.Application.Common.Security;
using Cartex.Application.Roles.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Entities;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class AssignableRolesTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task Role_with_assignable_list_can_only_assign_listed_roles()
    {
        long businessId, developerId, branch1, managerUserId, sellerRoleId, adminRoleId;
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            businessId = (await db.Businesses.FirstAsync()).Id;
            developerId = (await db.Users.FirstAsync(u => u.Username == "developer")).Id;
            branch1 = (await db.Branches.FirstAsync(b => b.Name == "Asosiy filial")).Id;
            sellerRoleId = (await db.Roles.FirstAsync(r => r.Name == "seller")).Id;
            adminRoleId = (await db.Roles.FirstAsync(r => r.Name == "admin")).Id;
        }

        Fixture.CurrentUser.AsAdmin(developerId, businessId, branch1);
        long managerRoleId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            managerRoleId = await sender.Send(new CreateRoleCommand("manager", null, null, 50, null, ["seller"]));

            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var manager = new User
            {
                Username = "manager1",
                FullName = "Manager",
                PasswordHash = "x",
                UserRoles = [new UserRole { RoleId = managerRoleId }]
            };
            db.Users.Add(manager);
            await db.SaveChangesAsync();
            managerUserId = manager.Id;
        }

        Fixture.CurrentUser.AsCashier(managerUserId, businessId, branch1);
        using var check = Fixture.CreateScope();
        var access = check.ServiceProvider.GetRequiredService<IAccessControlService>();

        await access.EnsureCanAssignRolesAsync([sellerRoleId], CancellationToken.None);
        await Assert.ThrowsAsync<ForbiddenException>(() => access.EnsureCanAssignRolesAsync([adminRoleId], CancellationToken.None));
    }
}
