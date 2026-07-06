using Cartex.Application.Tests.Common;
using Cartex.Domain.Authorization;
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
        using (var scope = fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Features
                .Where(f => f.Code == FeatureCatalog.Reports || f.Code == FeatureCatalog.Ordering)
                .ExecuteDeleteAsync();
        }

        using (var scope = fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await DatabaseSeeder.SyncFeaturesAsync(db);
        }

        using var check = fixture.CreateScope();
        var db2 = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reports = await db2.Features.FirstAsync(f => f.Code == FeatureCatalog.Reports);
        var ordering = await db2.Features.FirstAsync(f => f.Code == FeatureCatalog.Ordering);

        Assert.True(reports.IsEnabled);
        Assert.False(ordering.IsEnabled);
    }
}
