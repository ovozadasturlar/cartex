using Cartex.Application.Common.Messaging;
using Cartex.Application.Tests.Common;
using Cartex.Application.Users.Commands;
using Cartex.Domain.Common.Exceptions;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class UserBranchScopeTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task Branch_scoped_role_requires_at_least_one_branch()
    {
        var (developerId, businessId, branchId, sellerRoleId) = await ContextAsync();
        Fixture.CurrentUser.AsAdmin(developerId, businessId, branchId);

        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await Assert.ThrowsAsync<BusinessRuleException>(() => sender.Send(
            new CreateUserCommand(
                "Kirim operatori",
                $"supply-no-branch-{Guid.NewGuid():N}",
                "secret1",
                [sellerRoleId],
                null,
                [],
                "supplies")));
    }

    [Fact]
    public async Task Single_assigned_branch_becomes_default_automatically()
    {
        var (developerId, businessId, branchId, sellerRoleId) = await ContextAsync();
        Fixture.CurrentUser.AsAdmin(developerId, businessId, branchId);

        long userId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            userId = await sender.Send(new CreateUserCommand(
                "Kirim operatori",
                $"supply-one-branch-{Guid.NewGuid():N}",
                "secret1",
                [sellerRoleId],
                null,
                [branchId],
                "supplies"));
        }

        using var check = Fixture.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(branchId, await db.Users
            .Where(user => user.Id == userId)
            .Select(user => user.DefaultBranchId)
            .SingleAsync());
    }

    private async Task<(long DeveloperId, long BusinessId, long BranchId, long SellerRoleId)> ContextAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (
            await db.Users.Where(user => user.Username == "developer").Select(user => user.Id).SingleAsync(),
            await db.Businesses.Select(business => business.Id).FirstAsync(),
            await db.Branches.Select(branch => branch.Id).FirstAsync(),
            await db.Roles.Where(role => role.Name == "seller").Select(role => role.Id).SingleAsync());
    }
}
