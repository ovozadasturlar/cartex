using Cartex.Application.Tests.Common;
using Cartex.Domain.Authorization;
using Cartex.Domain.Entities;
using Cartex.Persistence;
using Cartex.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class FeatureSyncTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task Missing_features_are_recreated_with_default_enabled_state()
    {
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Features
                .Where(f => f.Code == FeatureCatalog.Reports || f.Code == FeatureCatalog.Ordering)
                .ExecuteDeleteAsync();
        }

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await DatabaseSeeder.SyncFeaturesAsync(db);
        }

        using var check = Fixture.CreateScope();
        var db2 = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reports = await db2.Features.FirstAsync(f => f.Code == FeatureCatalog.Reports);
        var ordering = await db2.Features.FirstAsync(f => f.Code == FeatureCatalog.Ordering);

        Assert.True(reports.IsEnabled);
        Assert.False(ordering.IsEnabled);
    }

    [Fact]
    public async Task Stale_features_not_in_catalog_are_removed()
    {
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Features.Add(new Feature { Code = "legacy_feature", Name = "Legacy", IsEnabled = true });
            await db.SaveChangesAsync();
        }

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await DatabaseSeeder.SyncFeaturesAsync(db);
        }

        using var check = Fixture.CreateScope();
        var db2 = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db2.Features.AnyAsync(f => f.Code == "legacy_feature"));
    }
}
