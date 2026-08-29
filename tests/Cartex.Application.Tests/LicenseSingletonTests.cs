using Cartex.Application.Tests.Common;
using Cartex.Domain.Entities;
using Cartex.Persistence;
using Cartex.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public sealed class LicenseSingletonTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task LITS_01_Second_licence_row_is_rejected_by_the_database()
    {
        var error = await InsertSecondRowAsync();

        Assert.StartsWith("23", error.SqlState);
        Assert.Equal("license_states", error.TableName);
        Assert.Equal(LicenseState.SingletonId, (await SingleAsync()).Id);
    }

    [Fact]
    public async Task LITS_01_Second_licence_row_is_rejected_through_the_entity_model_too()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.LicenseStates.Add(new LicenseState
        {
            Id = LicenseState.SingletonId + 1,
            Tariff = "free",
            CreatedAt = DateTime.UtcNow
        });

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var rejection = Assert.IsType<PostgresException>(error.InnerException);

        Assert.StartsWith("23", rejection.SqlState);
        Assert.Equal("license_states", rejection.TableName);
        Assert.Equal(LicenseState.SingletonId, (await SingleAsync()).Id);
    }

    [Fact]
    public async Task LITS_03_Reset_and_reseed_keeps_exactly_one_licence_row()
    {
        await Fixture.ResetAsync();
        await Fixture.ResetAsync();

        Assert.Equal(LicenseState.SingletonId, (await SingleAsync()).Id);
        Assert.Equal("license_states", (await InsertSecondRowAsync()).TableName);
    }

    [Fact]
    public async Task LITS_03_Reseeding_over_existing_data_updates_the_licence_row_instead_of_recreating_it()
    {
        var before = await SingleAsync();

        using (var scope = Fixture.CreateScope())
        {
            await DatabaseSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(), p => p);
        }

        var after = await SingleAsync();
        Assert.Equal(LicenseState.SingletonId, after.Id);
        Assert.Equal(before.CreatedAt, after.CreatedAt);
    }

    private async Task<PostgresException> InsertSecondRowAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
            "INSERT INTO license_states (id, tariff, created_at) VALUES (2, 'free', now())"));
    }

    private async Task<LicenseState> SingleAsync()
    {
        using var scope = Fixture.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .LicenseStates.AsNoTracking().SingleAsync();
    }
}
