using Cartex.Application.Tests.Common;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class AutomaticAuditTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task Entity_update_records_old_and_new_values()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branch = await db.Branches.FirstAsync();
        var user = await db.Users.FirstAsync(x => x.Username == "admin");
        var business = await db.Businesses.FirstAsync();
        Fixture.CurrentUser.AsAdmin(user.Id, business.Id, branch.Id);

        var oldName = branch.Name;
        branch.Name = "Audit test filial";
        await db.SaveChangesAsync();

        var audit = await db.AuditLogs
            .Where(x => x.Action == "EntityUpdated" && x.TableName == "branches" && x.RecordId == branch.Id)
            .OrderByDescending(x => x.Id)
            .FirstAsync();
        Assert.Contains(oldName, audit.OldData);
        Assert.Contains("Audit test filial", audit.NewData);
    }

    [Fact]
    public async Task Settings_value_is_redacted()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branch = await db.Branches.FirstAsync();
        var user = await db.Users.FirstAsync(x => x.Username == "admin");
        var business = await db.Businesses.FirstAsync();
        Fixture.CurrentUser.AsAdmin(user.Id, business.Id, branch.Id);

        var setting = await db.BusinessSettings.FirstAsync();
        setting.Value = """{"Password":"must-not-leak"}""";
        await db.SaveChangesAsync();

        var audit = await db.AuditLogs
            .Where(x => x.Action == "EntityUpdated" && x.TableName == "business_settings")
            .OrderByDescending(x => x.Id)
            .FirstAsync();
        Assert.DoesNotContain("must-not-leak", audit.NewData);
        Assert.Contains("[REDACTED]", audit.NewData);
    }
}
