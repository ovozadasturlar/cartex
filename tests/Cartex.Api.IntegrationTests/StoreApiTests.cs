using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cartex.Auth.Services;
using Cartex.Domain.Entities;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Api.IntegrationTests;

[Collection("api")]
public class StoreApiTests(CartexApiFactory factory)
{
    private sealed record StoreLogin(string Token, string RefreshToken, string FullName);
    private sealed record CatalogItem(long VariantId, string Name, string? VariantName, string Unit, decimal Price, bool Available);
    private sealed record StoreInfo(string BusinessName, string Currency, List<StoreWarehouse> Warehouses);
    private sealed record StoreWarehouse(long Id, string Name);
    private sealed record Order(string Code, string Status, DateTime CreatedAt, int ItemCount, string Warehouse);
    private sealed record Balance(decimal Debt, decimal Bonus, string Currency);

    private async Task<(long warehouseId, long variantId)> SeedCatalogAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var warehouse = await db.Warehouses.OrderBy(w => w.Id).FirstAsync();
        warehouse.IsOnline = true;
        var unitId = await db.Units.OrderBy(u => u.Id).Select(u => u.Id).FirstAsync();
        var baseCode = await db.Businesses.Select(b => b.Currency).FirstAsync();

        var product = new Product { Name = "Store Test Product", UnitId = unitId };
        var variant = new ProductVariant { IsDefault = true };
        product.Variants.Add(variant);
        db.Products.Add(product);
        await db.SaveChangesAsync();

        db.ProductPrices.Add(new ProductPrice { VariantId = variant.Id, WarehouseId = null, SellingPrice = 12000m, Currency = baseCode });
        db.Stocks.Add(new Stock { BranchId = warehouse.BranchId, VariantId = variant.Id, WarehouseId = warehouse.Id, Quantity = 50m, PurchasePrice = 9000m });
        await db.SaveChangesAsync();

        return (warehouse.Id, variant.Id);
    }

    private static async Task<string> SubmitAsync(HttpClient client, object body, string key)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/store/carts") { Content = JsonContent.Create(body) };
        req.Headers.Add("Idempotency-Key", key);
        var resp = await client.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadAsStringAsync()).Trim('"');
    }

    private async Task<StoreLogin> CustomerLoginAsync(string phone)
    {
        var dev = await AuthHelper.LoginAsync(factory, "developer", "developer123");
        (await dev.PutAsJsonAsync("/api/features/ordering", new { isEnabled = true })).EnsureSuccessStatusCode();
        (await dev.PutAsJsonAsync("/api/features/modules/ordering", new { isEnabled = true })).EnsureSuccessStatusCode();

        var admin = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        var created = await admin.PostAsJsonAsync("/api/customers", new { fullName = "Store " + phone, phone, discountPct = 0 });
        created.EnsureSuccessStatusCode();
        var customerId = await created.Content.ReadFromJsonAsync<long>();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            db.OtpChallenges.Add(new OtpChallenge { CustomerId = customerId, CodeHash = hasher.Hash("424242"), ExpiresAt = DateTime.UtcNow.AddMinutes(5) });
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        var verify = await client.PostAsJsonAsync("/api/store/auth/verify", new { phone, code = "424242", deviceName = "Store" });
        verify.EnsureSuccessStatusCode();
        return (await verify.Content.ReadFromJsonAsync<StoreLogin>())!;
    }

    [Fact]
    public async Task Catalog_And_Idempotent_Submit_And_Orders()
    {
        var (warehouseId, variantId) = await SeedCatalogAsync();
        var login = await CustomerLoginAsync("+998911002001");

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);

        var info = await client.GetFromJsonAsync<StoreInfo>("/api/store/info");
        Assert.Contains(info!.Warehouses, w => w.Id == warehouseId);

        var catalog = await client.GetFromJsonAsync<List<CatalogItem>>($"/api/store/catalog?warehouseId={warehouseId}");
        var item = catalog!.FirstOrDefault(c => c.VariantId == variantId);
        Assert.NotNull(item);
        Assert.Equal(12000m, item.Price);
        Assert.True(item.Available);

        var body = new { warehouseId, items = new[] { new { variantId, quantity = 2m } } };
        var code1 = await SubmitAsync(client, body, "order-key-1");
        var code2 = await SubmitAsync(client, body, "order-key-1");
        Assert.Equal(code1, code2);

        var orders = await client.GetFromJsonAsync<List<Order>>("/api/store/orders");
        Assert.Single(orders!, o => o.Code == code1);

        var balance = await client.GetFromJsonAsync<Balance>("/api/store/balance");
        Assert.Equal(0m, balance!.Debt);
        Assert.Equal(0m, balance.Bonus);
    }
}
