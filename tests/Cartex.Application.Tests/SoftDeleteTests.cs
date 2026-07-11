using Cartex.Application.Tests.Common;
using Cartex.Domain.Entities;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class SoftDeleteTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task Removed_entity_is_filtered_out_and_can_be_restored()
    {
        long categoryId;

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var category = new Category { Name = "TestCat-SoftDelete" };
            db.Categories.Add(category);
            await db.SaveChangesAsync();
            categoryId = category.Id;

            db.Categories.Remove(category);
            await db.SaveChangesAsync();
        }

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Null(await db.Categories.FirstOrDefaultAsync(c => c.Id == categoryId));

            var deleted = await db.Categories.IgnoreQueryFilters().FirstAsync(c => c.Id == categoryId);
            Assert.True(deleted.IsDeleted);

            deleted.IsDeleted = false;
            deleted.DeletedAt = null;
            await db.SaveChangesAsync();
        }

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.NotNull(await db.Categories.FirstOrDefaultAsync(c => c.Id == categoryId));
        }
    }
}
