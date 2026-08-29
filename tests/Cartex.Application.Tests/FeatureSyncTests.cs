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
    // SOZ-08b: qayta yaratilgan feature `IsEnabled` ni joriy tarifdan (bu yerda `pro` = hamma feature)
    // oladi, `OwnerEnabled` ni esa `DefaultDisabled` dan — ixtiyoriy modul (ordering) egа kaliti o'chiq,
    // yadro feature (reports) yoqiq.
    [Fact]
    public async Task Missing_features_are_recreated_with_tariff_licensed_and_owner_default_state()
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
        Assert.True(reports.OwnerEnabled);
        Assert.True(ordering.IsEnabled);
        Assert.False(ordering.OwnerEnabled);
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
