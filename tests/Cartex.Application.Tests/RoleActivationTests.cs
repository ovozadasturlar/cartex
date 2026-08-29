using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Security;
using Cartex.Application.Roles.Commands;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Entities;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public sealed class RoleActivationTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task Seeder_creates_active_business_roles_with_separated_sale_permissions()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roles = await db.Roles
            .Include(role => role.RolePermissions)
                .ThenInclude(rolePermission => rolePermission.Permission)
            .Where(role => new[]
            {
                AppRoles.Seller,
                AppRoles.SellerAssistant,
                AppRoles.Cashier,
                AppRoles.Accountant,
                AppRoles.WarehouseOperator,
                AppRoles.SupplyOperator,
                AppRoles.Agent
            }.Contains(role.Name))
            .ToDictionaryAsync(role => role.Name);

        Assert.Equal(7, roles.Count);
        Assert.All(roles.Values, role =>
        {
            Assert.True(role.IsSystem);
            Assert.True(role.IsActive);
        });

        var assistantPermissions = PermissionsOf(roles[AppRoles.SellerAssistant]);
        Assert.Contains(AppPermissions.Sales.Create, assistantPermissions);
        Assert.DoesNotContain(AppPermissions.Sales.Checkout, assistantPermissions);

        var cashierPermissions = PermissionsOf(roles[AppRoles.Cashier]);
        Assert.Contains(AppPermissions.Sales.Checkout, cashierPermissions);
        Assert.Contains(AppPermissions.Sales.Pick, cashierPermissions);
        Assert.DoesNotContain(AppPermissions.Sales.Create, cashierPermissions);

        var accountantPermissions = PermissionsOf(roles[AppRoles.Accountant]);
        Assert.Contains(AppPermissions.Accounts.View, accountantPermissions);
        Assert.Contains(AppPermissions.Transactions.View, accountantPermissions);
        Assert.Contains(AppPermissions.Reports.View, accountantPermissions);
        Assert.DoesNotContain(AppPermissions.Sales.Create, accountantPermissions);
        Assert.DoesNotContain(AppPermissions.Sales.Checkout, accountantPermissions);
    }

    [Fact]
    public async Task Deactivating_role_revokes_sessions_and_prevents_assignment()
    {
        long developerId;
        long businessId;
        long branchId;
        long assistantRoleId;
        long assistantUserId;
        long sessionId;

        using (var arrange = Fixture.CreateScope())
        {
            var db = arrange.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            developerId = (await db.Users.FirstAsync(user => user.Username == AppRoles.Developer)).Id;
            businessId = (await db.Businesses.FirstAsync()).Id;
            branchId = (await db.Branches.FirstAsync()).Id;
            assistantRoleId = (await db.Roles.FirstAsync(role => role.Name == AppRoles.SellerAssistant)).Id;

            var assistant = new User
            {
                Username = "assistant-role-test",
                FullName = "Assistant",
                PasswordHash = "hash",
                UserRoles = [new UserRole { RoleId = assistantRoleId }]
            };
            db.Users.Add(assistant);
            await db.SaveChangesAsync();
            assistantUserId = assistant.Id;

            var now = DateTime.UtcNow;
            var session = new RefreshSession
            {
                UserId = assistantUserId,
                TokenHash = Guid.NewGuid().ToString("N"),
                CreatedAt = now,
                FamilyCreatedAt = now,
                LastUsedAt = now,
                ExpiresAt = now.AddDays(1)
            };
            db.RefreshSessions.Add(session);
            await db.SaveChangesAsync();
            sessionId = session.Id;
        }

        Fixture.CurrentUser.AsAdmin(developerId, businessId, branchId);
        using (var act = Fixture.CreateScope())
        {
            var sender = act.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new SetRoleActiveCommand(assistantRoleId, false));
        }

        using var assertScope = Fixture.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False((await assertDb.Roles.FirstAsync(role => role.Id == assistantRoleId)).IsActive);
        Assert.NotNull((await assertDb.RefreshSessions.FirstAsync(session => session.Id == sessionId)).RevokedAt);

        var accessControl = assertScope.ServiceProvider.GetRequiredService<IAccessControlService>();
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            accessControl.EnsureCanAssignRolesAsync([assistantRoleId], CancellationToken.None));
    }

    [Fact]
    public async Task Direct_sale_requires_both_cart_creation_and_checkout_permissions()
    {
        long sellerId;
        long businessId;
        long branchId;
        using (var arrange = Fixture.CreateScope())
        {
            var db = arrange.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            sellerId = (await db.Users.FirstAsync(user => user.Username == AppRoles.Seller)).Id;
            businessId = (await db.Businesses.FirstAsync()).Id;
            branchId = (await db.Branches.FirstAsync()).Id;
        }

        Fixture.CurrentUser.AsCashier(sellerId, businessId, branchId);
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var request = new CreateSaleCommand(-1, null, 0, 0, 0, [new CreateSaleItemDto(-1, 1)]);

        Fixture.CurrentUser.Granted.Add(AppPermissions.Sales.Create);
        await Assert.ThrowsAsync<ForbiddenException>(() => sender.Send(request));

        Fixture.CurrentUser.Granted.Clear();
        Fixture.CurrentUser.Granted.Add(AppPermissions.Sales.Checkout);
        await Assert.ThrowsAsync<ForbiddenException>(() => sender.Send(request));
    }

    private static HashSet<string> PermissionsOf(Role role) =>
        role.RolePermissions
            .Select(rolePermission => rolePermission.Permission.Name)
            .ToHashSet();
}
