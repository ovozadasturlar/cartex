using Cartex.Application.Common.Messaging;
using Cartex.Application.Tests.Common;
using Cartex.Application.Users.Queries;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public sealed class UserQueryTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task User_list_projects_assigned_role_names()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var businessId = await db.Businesses.Select(x => x.Id).SingleAsync();
        var branchId = await db.Branches.Select(x => x.Id).FirstAsync();
        var adminId = await db.Users.Where(x => x.Username == "admin").Select(x => x.Id).SingleAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branchId);

        var users = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new GetUsersQuery());
        var admin = Assert.Single(users, x => x.Id == adminId);
        Assert.NotEmpty(admin.RoleIds);
        Assert.NotEmpty(admin.RoleNames);
        Assert.DoesNotContain(admin.RoleNames, string.IsNullOrWhiteSpace);
    }
}
