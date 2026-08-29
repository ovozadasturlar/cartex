using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Cartex.Api.IntegrationTests;

[Collection("api")]
public class OrderingPickerTests(CartexApiFactory factory)
{
    private sealed record IdName(long Id, string Name);
    private sealed record Product(long Id, long DefaultVariantId, string Name);
    private sealed record PermissionRow(long Id, string Name);
    private sealed record CartRow(long Id, string AggregateCode, string Status, string? CreatedByName, string? Note, decimal EstimatedTotal);

    private static async Task<HttpClient> CreatePickerAsync(CartexApiFactory factory)
    {
        var developer = await AuthHelper.LoginAsync(factory, "developer", "developer123");
        (await developer.PutAsJsonAsync("/api/features/store", new { isEnabled = true })).EnsureSuccessStatusCode();
        (await developer.PutAsJsonAsync("/api/features/modules/store", new { isEnabled = true })).EnsureSuccessStatusCode();

        var admin = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        var permissions = await admin.GetFromJsonAsync<List<PermissionRow>>("/api/permissions");
        var wanted = new[] { "sales.pick", "products.view", "customers.view", "warehouses.view" };
        var ids = permissions!.Where(p => wanted.Contains(p.Name)).Select(p => p.Id).ToList();

        var suffix = Random.Shared.Next(100_000, 999_999);
        var roleResp = await admin.PostAsJsonAsync("/api/roles", new { name = $"picker_{suffix}", description = (string?)null, startPage = (string?)null, priority = 1 });
        roleResp.EnsureSuccessStatusCode();
        var roleId = await roleResp.Content.ReadFromJsonAsync<long>();
        (await admin.PutAsJsonAsync($"/api/roles/{roleId}/permissions", new { roleId, permissionIds = ids })).EnsureSuccessStatusCode();

        var branches = await admin.GetFromJsonAsync<List<IdName>>("/api/branches");
        var userResp = await admin.PostAsJsonAsync("/api/users", new
        {
            fullName = $"Picker {suffix}",
            username = $"picker{suffix}",
            password = "picker123",
            roleIds = new[] { roleId },
            defaultBranchId = branches![0].Id,
            branchIds = new[] { branches[0].Id },
            startPage = (string?)null
        });
        userResp.EnsureSuccessStatusCode();

        return await AuthHelper.LoginAsync(factory, $"picker{suffix}", "picker123");
    }

    private static async Task<string> SubmitCartAsync(HttpClient client, string? note = null)
    {
        var warehouses = await client.GetFromJsonAsync<List<IdName>>("/api/warehouses");
        var products = await client.GetFromJsonAsync<List<Product>>("/api/products");
        var mixer = products!.First(p => p.Name == "Smesitel oshxona Zegor");
        var resp = await client.PostAsJsonAsync("/api/ordering/carts", new
        {
            warehouseId = warehouses![0].Id,
            customerId = (long?)null,
            items = new[] { new { variantId = mixer.DefaultVariantId, quantity = 2m } },
            note
        });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadAsStringAsync()).Trim('"');
    }

    [Fact]
    public async Task Picker_submits_cart_and_sees_only_own()
    {
        var picker = await CreatePickerAsync(factory);
        var admin = await AuthHelper.LoginAsync(factory, "admin", "admin123");

        var ownCode = await SubmitCartAsync(picker, "tez kerak");
        var otherCode = await SubmitCartAsync(admin);

        var pickerCarts = await picker.GetFromJsonAsync<List<CartRow>>("/api/ordering/carts");
        Assert.Contains(pickerCarts!, c => c.AggregateCode == ownCode);
        Assert.DoesNotContain(pickerCarts!, c => c.AggregateCode == otherCode);

        var own = pickerCarts!.First(c => c.AggregateCode == ownCode);
        Assert.Equal("tez kerak", own.Note);
        Assert.True(own.EstimatedTotal > 0);
        Assert.False(string.IsNullOrEmpty(own.CreatedByName));

        var adminCarts = await admin.GetFromJsonAsync<List<CartRow>>("/api/ordering/carts");
        Assert.Contains(adminCarts!, c => c.AggregateCode == ownCode);

        Assert.Equal(HttpStatusCode.NotFound, (await picker.GetAsync($"/api/ordering/carts/{otherCode}")).StatusCode);
        (await picker.GetAsync($"/api/ordering/carts/{ownCode}")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Picker_cannot_checkout()
    {
        var picker = await CreatePickerAsync(factory);
        var code = await SubmitCartAsync(picker);
        var resp = await picker.PostAsJsonAsync($"/api/ordering/carts/{code}/checkout", new { paidCash = 1_000m, paidCard = 0m, paidBonus = 0m });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Picker_can_cancel_only_own_open_cart()
    {
        var picker = await CreatePickerAsync(factory);
        var admin = await AuthHelper.LoginAsync(factory, "admin", "admin123");

        var ownCode = await SubmitCartAsync(picker);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await picker.PutAsJsonAsync($"/api/ordering/carts/{ownCode}/status", new { status = "Confirmed" })).StatusCode);
        (await picker.PutAsJsonAsync($"/api/ordering/carts/{ownCode}/status", new { status = "Cancelled" })).EnsureSuccessStatusCode();

        var otherCode = await SubmitCartAsync(admin);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await picker.PutAsJsonAsync($"/api/ordering/carts/{otherCode}/status", new { status = "Cancelled" })).StatusCode);
    }

    [Fact]
    public async Task Picker_can_lookup_by_barcode()
    {
        var picker = await CreatePickerAsync(factory);
        var warehouses = await picker.GetFromJsonAsync<List<IdName>>("/api/warehouses");
        var resp = await picker.GetAsync($"/api/products/by-barcode?code=0000000000000&warehouseId={warehouses![0].Id}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Cashier_flow_still_works()
    {
        var developer = await AuthHelper.LoginAsync(factory, "developer", "developer123");
        (await developer.PutAsJsonAsync("/api/features/store", new { isEnabled = true })).EnsureSuccessStatusCode();
        (await developer.PutAsJsonAsync("/api/features/modules/store", new { isEnabled = true })).EnsureSuccessStatusCode();

        var admin = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        await AuthHelper.EnsureOpenShiftAsync(admin);
        var code = await SubmitCartAsync(admin);
        (await admin.PutAsJsonAsync($"/api/ordering/carts/{code}/status", new { status = "Confirmed" })).EnsureSuccessStatusCode();
        var cart = await admin.GetFromJsonAsync<Dictionary<string, object>>($"/api/ordering/carts/{code}");
        Assert.NotNull(cart);
        var checkout = await admin.PostAsJsonAsync($"/api/ordering/carts/{code}/checkout", new { paidCash = 1_000_000m, paidCard = 0m, paidBonus = 0m });
        checkout.EnsureSuccessStatusCode();
    }
}
