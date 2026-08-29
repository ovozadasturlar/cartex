using System.Net.Http.Json;
using Xunit;

namespace Cartex.Api.IntegrationTests;

[Collection("api")]
public class AgentFoundationTests(CartexApiFactory factory)
{
    private sealed record IdName(long Id, string Name);
    private sealed record Product(long Id, long DefaultVariantId, string Name);
    private sealed record SaleResult(long SaleId, string ReceiptToken);
    private sealed record Stock(long VariantId, decimal Quantity);

    [Fact]
    public async Task Sale_WithIdempotencyKey_IsNotDuplicated()
    {
        var client = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        await AuthHelper.EnsureOpenShiftAsync(client);

        var warehouses = await client.GetFromJsonAsync<List<IdName>>("/api/warehouses");
        var products = await client.GetFromJsonAsync<List<Product>>("/api/products?search=TEN");
        var heater = products!.First(p => p.Name == "TEN 1.5kVt suv isitgich uchun");
        var key = Guid.NewGuid().ToString("N");

        object body = new
        {
            warehouseId = warehouses![0].Id,
            customerId = (long?)null,
            paidCash = 300_000m,
            paidCard = 0m,
            paidBonus = 0m,
            items = new[] { new { variantId = heater.DefaultVariantId, quantity = 2m } },
            discountAmount = 0m,
            idempotencyKey = key
        };

        var first = await client.PostAsJsonAsync("/api/sales", body);
        first.EnsureSuccessStatusCode();
        var firstResult = (await first.Content.ReadFromJsonAsync<SaleResult>())!;

        var second = await client.PostAsJsonAsync("/api/sales", body);
        second.EnsureSuccessStatusCode();
        var secondResult = (await second.Content.ReadFromJsonAsync<SaleResult>())!;

        Assert.Equal(firstResult.SaleId, secondResult.SaleId);
        Assert.Equal(firstResult.ReceiptToken, secondResult.ReceiptToken);
    }

    [Fact]
    public async Task Repay_WithIdempotencyKey_IsNotDuplicated()
    {
        var client = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        await AuthHelper.EnsureOpenShiftAsync(client);

        var create = await client.PostAsJsonAsync("/api/customers", new
        {
            fullName = "Agent Repay Test",
            phone = "+998" + Random.Shared.NextInt64(100_000_000, 999_999_999),
            cardBarcode = (string?)null,
            discountPct = 0m,
            creditLimit = 10_000_000m,
            openingBalance = 50_000m
        });
        create.EnsureSuccessStatusCode();
        var customerId = await create.Content.ReadFromJsonAsync<long>();

        var key = Guid.NewGuid().ToString("N");
        object body = new { amount = 20_000m, viaCard = false, idempotencyKey = key };

        (await client.PostAsJsonAsync($"/api/customers/{customerId}/repay-debt", body)).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync($"/api/customers/{customerId}/repay-debt", body)).EnsureSuccessStatusCode();

        var customers = await client.GetFromJsonAsync<List<CustRow>>($"/api/customers?search=Agent Repay Test");
        Assert.Equal(30_000m, customers!.First(c => c.Id == customerId).DebtBalance);
    }

    private sealed record CustRow(long Id, string FullName, decimal DebtBalance);

    [Fact]
    public async Task AgentBootstrap_ReturnsAssignedCustomersAndVanStock()
    {
        var dev = await AuthHelper.LoginAsync(factory, "developer", "developer123");
        (await dev.PutAsJsonAsync("/api/features/agents", new { isEnabled = true })).EnsureSuccessStatusCode();
        (await dev.PutAsJsonAsync("/api/features/modules/agents", new { isEnabled = true })).EnsureSuccessStatusCode();

        var admin = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        long? assignedWarehouseId = null;
        string? assignedWarehouseName = null;
        try
        {
            var warehouses = await admin.GetFromJsonAsync<List<IdName>>("/api/warehouses");
            var warehouseId = warehouses![0].Id;
            assignedWarehouseId = warehouseId;
            assignedWarehouseName = warehouses[0].Name;

            var adminId = await GetUserIdAsync(admin, "admin");
            (await admin.PutAsJsonAsync($"/api/warehouses/{warehouseId}",
                new { name = warehouses[0].Name, isOnline = false, assignedUserId = adminId })).EnsureSuccessStatusCode();

            var createCustomer = await admin.PostAsJsonAsync("/api/customers", new
            {
                fullName = "Agent Do'kon Test",
                phone = "+998" + Random.Shared.NextInt64(100_000_000, 999_999_999),
                cardBarcode = (string?)null,
                discountPct = 0m,
                agentId = adminId
            });
            createCustomer.EnsureSuccessStatusCode();

            var bootstrap = await admin.GetFromJsonAsync<BootstrapDto>("/api/agent/bootstrap");
            Assert.Equal(warehouseId, bootstrap!.WarehouseId);
            Assert.Contains(bootstrap.Customers, c => c.FullName == "Agent Do'kon Test");
            Assert.NotEmpty(bootstrap.VanStock);
        }
        finally
        {
            if (assignedWarehouseId is not null)
                (await admin.PutAsJsonAsync($"/api/warehouses/{assignedWarehouseId}",
                    new { name = assignedWarehouseName, isOnline = false, assignedUserId = 0 })).EnsureSuccessStatusCode();
            (await dev.PutAsJsonAsync("/api/features/agents", new { isEnabled = false })).EnsureSuccessStatusCode();
        }
    }

    private sealed record BootstrapDto(long? WarehouseId, string? WarehouseName, string BaseCurrency, DateTime ServerTime, List<BootCustomer> Customers, List<Stock> VanStock);
    private sealed record BootCustomer(long Id, string FullName);

    private static async Task<long> GetUserIdAsync(HttpClient client, string username)
    {
        var users = await client.GetFromJsonAsync<List<UserRow>>("/api/users");
        return users!.First(u => u.Username == username).Id;
    }

    private sealed record UserRow(long Id, string Username);
}
