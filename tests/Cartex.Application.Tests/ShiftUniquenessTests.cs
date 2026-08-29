using Cartex.Application.Common.Messaging;
using Cartex.Application.Shifts.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public sealed class ShiftUniquenessTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private long _branchId;
    private long _adminId;

    private async Task StartAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        _branchId = await db.Branches.Where(x => x.Name == "Asosiy filial").Select(x => x.Id).FirstAsync();
        var businessId = await db.Businesses.Select(x => x.Id).FirstAsync();
        _adminId = await db.Users.Where(x => x.Username == "admin").Select(x => x.Id).FirstAsync();
        Fixture.CurrentUser.AsAdmin(_adminId, businessId, _branchId);
    }

    private async Task<long?> TryOpenAsync()
    {
        try
        {
            using var scope = Fixture.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new OpenShiftCommand(0));
        }
        catch
        {
            return null;
        }
    }

    private async Task<int> OpenShiftCountAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Shifts.CountAsync(s => s.UserId == _adminId && s.BranchId == _branchId && s.Status == ShiftStatus.Open);
    }

    // SMENA-01
    [Fact]
    public async Task SMENA_01_second_open_attempt_does_not_create_a_second_open_shift()
    {
        await StartAsync();

        var first = await TryOpenAsync();
        Assert.NotNull(first);

        var second = await TryOpenAsync();

        Assert.Equal(1, await OpenShiftCountAsync());
        if (second is not null) Assert.Equal(first, second);
    }

    // SMENA-01
    [Fact]
    public async Task SMENA_01_parallel_open_attempts_do_not_create_a_second_open_shift()
    {
        await StartAsync();

        var results = await Task.WhenAll(TryOpenAsync(), TryOpenAsync());

        Assert.Equal(1, await OpenShiftCountAsync());
        Assert.Contains(results, x => x is not null);
        Assert.Single(results.Where(x => x is not null).Select(x => x!.Value).Distinct());
    }

    // SMENA-01
    [Fact]
    public async Task SMENA_01_database_holds_a_partial_unique_index_for_one_open_shift()
    {
        await StartAsync();

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var table = db.Model.FindEntityType(typeof(Shift))!.GetTableName()!;
        var definitions = await db.Database
            .SqlQuery<string>($"select indexdef as \"Value\" from pg_indexes where tablename = {table}")
            .ToListAsync();

        Assert.Contains(definitions, d =>
            d.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
            && d.Contains("WHERE", StringComparison.OrdinalIgnoreCase)
            && d.Contains("user_id", StringComparison.OrdinalIgnoreCase)
            && d.Contains("branch_id", StringComparison.OrdinalIgnoreCase));
    }
}
