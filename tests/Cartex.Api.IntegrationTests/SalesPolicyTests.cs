using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Cartex.Api.IntegrationTests;

[Collection("api")]
public class SalesPolicyTests(CartexApiFactory factory)
{
    private sealed record IdName(long Id, string Name);
    private sealed record UserRow(long Id, string Username);
    private sealed record Product(long Id, long DefaultVariantId, string Name);

    private static Task<HttpResponseMessage> SetPolicyAsync(HttpClient client, string shiftPolicy, decimal maxDiscount) =>
        client.PutAsJsonAsync("/api/settings/sales-policy", new
        {
            shiftPolicy,
            maxDiscountPercent = maxDiscount,
            defaultMinStock = 0m,
            staleRateDays = 3
        });

    private static async Task<(long warehouseId, long variantId)> LookupAsync(HttpClient client)
    {
        var warehouses = await client.GetFromJsonAsync<List<IdName>>("/api/warehouses");
        var products = await client.GetFromJsonAsync<List<Product>>("/api/products");
        return (warehouses![0].Id, products!.First(p => p.Name == "Coca-Cola 1.5L").DefaultVariantId);
    }

    private static Task<HttpResponseMessage> SellAsync(HttpClient client, long warehouseId, long variantId, decimal paidCash, decimal discount = 0) =>
        client.PostAsJsonAsync("/api/sales", new
        {
            warehouseId,
            customerId = (long?)null,
            paidCash,
            paidCard = 0m,
            paidBonus = 0m,
            items = new[] { new { variantId, quantity = 1m } },
            discountAmount = discount
        });

    [Fact]
    public async Task Discount_above_policy_limit_is_rejected_for_seller()
    {
        var admin = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        var seller = await AuthHelper.LoginAsync(factory, "seller", "seller123");
        await AuthHelper.EnsureOpenShiftAsync(seller);
        var (warehouseId, variantId) = await LookupAsync(admin);
        try
        {
            (await SetPolicyAsync(admin, "CashOnly", 5)).EnsureSuccessStatusCode();
            var response = await SellAsync(seller, warehouseId, variantId, 5_000m, 5_000m);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            (await SetPolicyAsync(admin, "CashOnly", 0)).EnsureSuccessStatusCode();
        }
    }

    [Fact]
    public async Task Own_van_warehouse_cash_sale_needs_no_shift()
    {
        var admin = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        await AuthHelper.EnsureNoOpenShiftAsync(admin);
        var (warehouseId, variantId) = await LookupAsync(admin);
        var warehouses = await admin.GetFromJsonAsync<List<IdName>>("/api/warehouses");
        var users = await admin.GetFromJsonAsync<List<UserRow>>("/api/users");
        var adminId = users!.First(u => u.Username == "admin").Id;
        try
        {
            var blocked = await SellAsync(admin, warehouseId, variantId, 100_000m);
            Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);

            (await admin.PutAsJsonAsync($"/api/warehouses/{warehouseId}",
                new { name = warehouses![0].Name, isOnline = false, assignedUserId = adminId })).EnsureSuccessStatusCode();

            var response = await SellAsync(admin, warehouseId, variantId, 100_000m);
            response.EnsureSuccessStatusCode();
        }
        finally
        {
            (await admin.PutAsJsonAsync($"/api/warehouses/{warehouseId}",
                new { name = warehouses![0].Name, isOnline = false, assignedUserId = 0 })).EnsureSuccessStatusCode();
        }
    }

    [Fact]
    public async Task Shift_policy_off_allows_cash_sale_without_shift()
    {
        var admin = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        await AuthHelper.EnsureNoOpenShiftAsync(admin);
        var (warehouseId, variantId) = await LookupAsync(admin);
        try
        {
            (await SetPolicyAsync(admin, "Off", 0)).EnsureSuccessStatusCode();
            var response = await SellAsync(admin, warehouseId, variantId, 100_000m);
            response.EnsureSuccessStatusCode();
        }
        finally
        {
            (await SetPolicyAsync(admin, "CashOnly", 0)).EnsureSuccessStatusCode();
        }
    }

    [Fact]
    public async Task Receipt_settings_roundtrip()
    {
        var admin = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        (await admin.PutAsJsonAsync("/api/settings/receipt", new { headerText = "INN 123", footerText = "Yana keling!", paperWidth = 42 })).EnsureSuccessStatusCode();
        var cfg = await admin.GetFromJsonAsync<Dictionary<string, object>>("/api/settings/receipt");
        Assert.Equal("Yana keling!", cfg!["footerText"].ToString());
        Assert.Equal("42", cfg["paperWidth"].ToString());
    }
}
