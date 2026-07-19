using System.Net.Http.Json;
using Xunit;

namespace Cartex.Api.IntegrationTests;

[Collection("api")]
public class DebtApiTests(CartexApiFactory factory)
{
    private sealed record IdName(long Id, string Name);
    private sealed record Product(long Id, long DefaultVariantId, string Name);
    private sealed record Cust(long Id, string FullName, decimal CashbackBalance, decimal DebtBalance);

    private static async Task<long> CreateCustomerAsync(HttpClient client, string name)
    {
        var resp = await client.PostAsJsonAsync("/api/customers", new
        {
            fullName = name,
            phone = "+998" + Random.Shared.NextInt64(100_000_000, 999_999_999),
            cardBarcode = (string?)null,
            discountPct = 0m,
            creditLimit = 10_000_000m
        });
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<long>();
    }

    private static async Task<Cust> GetCustomerAsync(HttpClient client, long id, string name)
    {
        var customers = await client.GetFromJsonAsync<List<Cust>>($"/api/customers?search={Uri.EscapeDataString(name)}");
        return customers!.First(c => c.Id == id);
    }

    private static async Task CreditSaleAsync(HttpClient client, long customerId)
    {
        var warehouses = await client.GetFromJsonAsync<List<IdName>>("/api/warehouses");
        var products = await client.GetFromJsonAsync<List<Product>>("/api/products");
        var mixer = products!.First(p => p.Name == "Smesitel oshxona Zegor");
        var resp = await client.PostAsJsonAsync("/api/sales", new
        {
            warehouseId = warehouses![0].Id,
            customerId,
            paidCash = 0m,
            paidCard = 0m,
            paidBonus = 0m,
            items = new[] { new { variantId = mixer.DefaultVariantId, quantity = 2m } },
            discountAmount = 0m
        });
        resp.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Repay_debt_via_api_reduces_debt_to_zero()
    {
        var client = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        await AuthHelper.EnsureOpenShiftAsync(client);
        var name = "Qarz API Mijoz " + Guid.NewGuid().ToString("N");
        var customerId = await CreateCustomerAsync(client, name);
        await CreditSaleAsync(client, customerId);

        var debt = (await GetCustomerAsync(client, customerId, name)).DebtBalance;
        Assert.True(debt > 0);

        var repay = await client.PostAsJsonAsync($"/api/customers/{customerId}/repay-debt", new { amount = debt, viaCard = false });
        repay.EnsureSuccessStatusCode();

        Assert.Equal(0m, (await GetCustomerAsync(client, customerId, name)).DebtBalance);
    }

    [Fact]
    public async Task Repay_more_than_debt_is_rejected_and_debt_unchanged()
    {
        var client = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        await AuthHelper.EnsureOpenShiftAsync(client);
        var name = "Qarz API Rad " + Guid.NewGuid().ToString("N");
        var customerId = await CreateCustomerAsync(client, name);
        await CreditSaleAsync(client, customerId);

        var debt = (await GetCustomerAsync(client, customerId, name)).DebtBalance;

        var repay = await client.PostAsJsonAsync($"/api/customers/{customerId}/repay-debt", new { amount = debt + 1m, viaCard = false });
        Assert.False(repay.IsSuccessStatusCode);
        Assert.Equal(debt, (await GetCustomerAsync(client, customerId, name)).DebtBalance);
    }

    [Fact]
    public async Task Give_bonus_via_api_increases_cashback_balance()
    {
        var client = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        await AuthHelper.EnsureOpenShiftAsync(client);
        var name = "Bonus API Mijoz " + Guid.NewGuid().ToString("N");
        var customerId = await CreateCustomerAsync(client, name);

        var resp = await client.PostAsJsonAsync($"/api/customers/{customerId}/bonus", new { amount = 5000m, note = "sovg'a" });
        resp.EnsureSuccessStatusCode();

        Assert.Equal(5000m, (await GetCustomerAsync(client, customerId, name)).CashbackBalance);
    }
}
