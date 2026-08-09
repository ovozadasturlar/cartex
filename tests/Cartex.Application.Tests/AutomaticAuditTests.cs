using Cartex.Application.Tests.Common;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Products.Commands;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class AutomaticAuditTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task One_command_with_multiple_saves_records_one_aggregate_event()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var branch = await db.Branches.FirstAsync();
        var user = await db.Users.FirstAsync(x => x.Username == "admin");
        var business = await db.Businesses.FirstAsync();
        var unitId = await db.Units.Where(x => x.ShortName == "dona").Select(x => x.Id).FirstAsync();
        Fixture.CurrentUser.AsAdmin(user.Id, business.Id, branch.Id);
        var previousMaxId = await db.AuditLogs.Select(x => (long?)x.Id).MaxAsync() ?? 0;

        await sender.Send(new CreateProductCommand("Audit aggregate product", null, unitId, 0, null));

        var logs = await db.AuditLogs.Where(x => x.Id > previousMaxId).ToListAsync();
        var log = Assert.Single(logs);
        Assert.Equal("command.create_product", log.Action);
        Assert.Equal("CreateProductCommand", log.CommandName);
        Assert.True(log.EntityCount >= 3);
        Assert.Contains("products", log.Details);
        Assert.Contains("product_variants", log.Details);
        Assert.Contains("barcodes", log.Details);
    }

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
