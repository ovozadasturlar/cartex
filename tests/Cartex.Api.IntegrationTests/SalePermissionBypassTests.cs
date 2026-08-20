using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Cartex.Api.IntegrationTests;

[Collection("api")]
public class SalePermissionBypassTests(CartexApiFactory factory)
{
    private sealed record IdName(long Id, string Name);
    private sealed record Product(long Id, long DefaultVariantId, string Name);
    private sealed record PermissionRow(long Id, string Name);

    private static readonly string[] CheckoutOnly =
    [
        "sales.checkout", "sales.pick", "products.view", "stocks.view", "categories.view",
        "customers.view", "rates.view", "warehouses.view", "shifts.open", "shifts.close", "shifts.view"
    ];

    private static async Task<HttpClient> CreateCheckoutOnlyUserAsync(CartexApiFactory factory)
    {
        var developer = await AuthHelper.LoginAsync(factory, "developer", "developer123");
        (await developer.PutAsJsonAsync("/api/features/store", new { isEnabled = true })).EnsureSuccessStatusCode();

        var admin = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        var permissions = await admin.GetFromJsonAsync<List<PermissionRow>>("/api/permissions");
        var ids = permissions!.Where(p => CheckoutOnly.Contains(p.Name)).Select(p => p.Id).ToList();
        var createId = permissions!.Single(p => p.Name == "sales.create").Id;
        Assert.DoesNotContain(createId, ids);

        var suffix = Random.Shared.Next(100_000, 999_999);
        var roleResp = await admin.PostAsJsonAsync("/api/roles",
            new { name = $"checkout_{suffix}", description = (string?)null, startPage = (string?)null, priority = 1 });
        roleResp.EnsureSuccessStatusCode();
        var roleId = await roleResp.Content.ReadFromJsonAsync<long>();
        (await admin.PutAsJsonAsync($"/api/roles/{roleId}/permissions", new { roleId, permissionIds = ids }))
            .EnsureSuccessStatusCode();

        var branches = await admin.GetFromJsonAsync<List<IdName>>("/api/branches");
        (await admin.PostAsJsonAsync("/api/users", new
        {
            fullName = $"Kassir {suffix}",
            username = $"checkout{suffix}",
            password = "checkout123",
            roleIds = new[] { roleId },
            defaultBranchId = branches![0].Id,
            branchIds = new[] { branches[0].Id },
            startPage = (string?)null
        })).EnsureSuccessStatusCode();

        return await AuthHelper.LoginAsync(factory, $"checkout{suffix}", "checkout123");
    }

    private static async Task<(long WarehouseId, long VariantId)> CatalogAsync(HttpClient client)
    {
        var warehouses = await client.GetFromJsonAsync<List<IdName>>("/api/warehouses");
        var products = await client.GetFromJsonAsync<List<Product>>("/api/products");
        var product = products!.First(p => p.Name == "Mufta PPR 20mm");
        return (warehouses![0].Id, product.DefaultVariantId);
    }

    // RUXSAT-06
    [Fact]
    public async Task RUXSAT_06_sale_is_refused_without_sales_create_permission()
    {
        var client = await CreateCheckoutOnlyUserAsync(factory);
        await AuthHelper.EnsureOpenShiftAsync(client);
        var (warehouseId, variantId) = await CatalogAsync(client);

        var response = await client.PostAsJsonAsync("/api/sales", new
        {
            warehouseId,
            customerId = (long?)null,
            paidCash = 100_000m,
            paidCard = 0m,
            paidBonus = 0m,
            items = new[] { new { variantId, quantity = 1m } }
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // RUXSAT-05
    [Fact]
    public async Task RUXSAT_05_queued_checkout_flag_in_the_body_does_not_bypass_sales_create()
    {
        var client = await CreateCheckoutOnlyUserAsync(factory);
        await AuthHelper.EnsureOpenShiftAsync(client);
        var (warehouseId, variantId) = await CatalogAsync(client);

        var body = new
        {
            warehouseId,
            customerId = (long?)null,
            paidCash = 100_000m,
            paidCard = 0m,
            paidBonus = 0m,
            items = new[] { new { variantId, quantity = 1m } },
            fromQueuedCart = true
        };

        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/sales", body)).StatusCode);

        // RUXSAT-06: the same body is accepted for a user who does hold sales.create.
        var admin = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        await AuthHelper.EnsureOpenShiftAsync(admin);
        (await admin.PostAsJsonAsync("/api/sales", body)).EnsureSuccessStatusCode();
    }

    // RUXSAT-06
    [Fact]
    public async Task RUXSAT_06_queued_cart_checkout_still_works_without_sales_create()
    {
        var client = await CreateCheckoutOnlyUserAsync(factory);
        await AuthHelper.EnsureOpenShiftAsync(client);
        var (warehouseId, variantId) = await CatalogAsync(client);

        var submit = await client.PostAsJsonAsync("/api/ordering/carts", new
        {
            warehouseId,
            customerId = (long?)null,
            items = new[] { new { variantId, quantity = 1m } }
        });
        submit.EnsureSuccessStatusCode();
        var code = (await submit.Content.ReadAsStringAsync()).Trim('"');

        var checkout = await client.PostAsJsonAsync($"/api/ordering/carts/{code}/checkout",
            new { paidCash = 100_000m, paidCard = 0m, paidBonus = 0m });

        checkout.EnsureSuccessStatusCode();
    }
}
