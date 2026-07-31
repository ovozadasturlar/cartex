using Cartex.Application.Roles.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Cartex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Cartex.Persistence.Seed;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class RolePermissionDependencyTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task Legacy_manage_permissions_migrate_without_duplicate_dependencies()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var role = new Role { Name = $"legacy-{Guid.NewGuid():N}"[..24], Priority = 1 };
        var legacy = new[]
        {
            new Permission { Name = "products.manage", Description = "legacy" },
            new Permission { Name = "supplies.manage", Description = "legacy" },
            new Permission { Name = "branches.manage", Description = "legacy" }
        };
        db.Roles.Add(role);
        db.Permissions.AddRange(legacy);
        await db.SaveChangesAsync();
        db.RolePermissions.AddRange(legacy.Select(permission =>
            new RolePermission { RoleId = role.Id, PermissionId = permission.Id }));
        await db.SaveChangesAsync();

        await DatabaseSeeder.SyncPermissionsAsync(db);

        var names = await db.RolePermissions
            .Where(rp => rp.RoleId == role.Id)
            .Select(rp => rp.Permission.Name)
            .ToListAsync();
        Assert.DoesNotContain(names, name => name.EndsWith(".manage", StringComparison.Ordinal));
        Assert.Contains("products.edit", names);
        Assert.Contains("supplies.edit", names);
        Assert.Contains("supplies.create", names);
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [Fact]
    public async Task Granting_users_edit_also_grants_its_dependencies()
    {
        long businessId, developerId, branch1, sellerRoleId, usersEditId;
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            businessId = (await db.Businesses.FirstAsync()).Id;
            developerId = (await db.Users.FirstAsync(u => u.Username == "developer")).Id;
            branch1 = (await db.Branches.FirstAsync(b => b.Name == "Asosiy filial")).Id;
            sellerRoleId = (await db.Roles.FirstAsync(r => r.Name == "seller")).Id;
            usersEditId = (await db.Permissions.FirstAsync(p => p.Name == "users.edit")).Id;
        }
        Fixture.CurrentUser.AsAdmin(developerId, businessId, branch1);

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new AssignPermissionsCommand(sellerRoleId, [usersEditId]));
        }

        using var check = Fixture.CreateScope();
        var db2 = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var names = await db2.RolePermissions
            .Where(rp => rp.RoleId == sellerRoleId)
            .Select(rp => rp.Permission.Name)
            .ToListAsync();

        Assert.Contains("users.edit", names);
        Assert.Contains("users.view", names);
        Assert.Contains("roles.view", names);
        Assert.Contains("branches.view", names);
    }
}
