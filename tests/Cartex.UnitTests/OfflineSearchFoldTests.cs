using Cartex.UI.Services;
using SQLite;
using Xunit;

namespace Cartex.UnitTests;

public sealed class OfflineSearchFoldTests
{
    [Fact]
    public async Task WP10_Offline_product_search_uses_the_shared_folding_and_strict_ranking()
    {
        var directory = Path.Combine(Path.GetTempPath(), "cartex-search-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var store = new OfflineStore(Path.Combine(directory, "offline.db3"));
            await store.ReplaceSnapshotAsync(
                [
                    Product(1, "Kora bo'yoq"),
                    Product(2, "Qora bo'yoq"),
                    Product(3, "Gisht")
                ],
                [],
                [],
                [],
                1,
                1,
                1);

            var qResults = await store.SearchProductsAsync("қора", 10);
            var brickResults = await store.SearchProductsAsync("Ғишт", 10);

            Assert.Equal(["Qora bo'yoq", "Kora bo'yoq"], qResults.Select(x => x.ProductName).ToArray());
            Assert.Equal("Gisht", Assert.Single(brickResults).ProductName);
        }
        finally
        {
            SQLiteAsyncConnection.ResetPool();
            Directory.Delete(directory, true);
        }
    }

    private static OfflineProduct Product(long id, string name) => new()
    {
        VariantId = id,
        ProductName = name,
        UnitName = "dona",
        Quantity = 1,
        SellingPrice = 1
    };
}
