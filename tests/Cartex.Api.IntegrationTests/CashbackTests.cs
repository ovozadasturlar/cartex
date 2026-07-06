using System.Net.Http.Json;
using Xunit;

namespace Cartex.Api.IntegrationTests;

[Collection("api")]
public class CashbackTests(CartexApiFactory factory)
{
    private sealed record IdName(long Id, string Name);
    private sealed record Product(long Id, long DefaultVariantId, string Name);
    private sealed record Customer(long Id, string FullName, decimal CashbackBalance);
    private sealed record Rule(long Id, string Scope, long TargetId, string TargetName, string Method, decimal Value, int Priority);
    private sealed record Program(bool IsEnabled, string Base, decimal TotalPercent, List<Rule> Rules);

    [Fact]
    public async Task PerProductRule_AwardsCashback_ToCustomerBonus()
    {
        var client = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        await AuthHelper.EnsureOpenShiftAsync(client);
        var warehouses = await client.GetFromJsonAsync<List<IdName>>("/api/warehouses");
        var warehouseId = warehouses![0].Id;

        var products = await client.GetFromJsonAsync<List<Product>>("/api/products");
        var cola = products!.First(p => p.Name == "Coca-Cola 1.5L");

        var custResp = await client.PostAsJsonAsync("/api/customers", new
        {
            fullName = "Cashback Test Customer",
            phone = "+998900000777",
            cardBarcode = (string?)null,
            discountPct = 0m,
            email = (string?)null
        });
        custResp.EnsureSuccessStatusCode();
        var customerId = await custResp.Content.ReadFromJsonAsync<long>();

        (await client.PutAsJsonAsync("/api/loyalty", new { isEnabled = true, @base = "PerLineRules", totalPercent = 0m }))
            .EnsureSuccessStatusCode();

        var ruleResp = await client.PostAsJsonAsync("/api/loyalty/rules", new
        {
            scope = "Product",
            targetId = cola.Id,
            method = "Percent",
            value = 7m,
            priority = 1
        });
        ruleResp.EnsureSuccessStatusCode();

        var program = await client.GetFromJsonAsync<Program>("/api/loyalty");
        Assert.Contains(program!.Rules, r => r.TargetName == "Coca-Cola 1.5L" && r.Value == 7m);

        var saleResp = await client.PostAsJsonAsync("/api/sales", new
        {
            warehouseId,
            customerId,
            paidCash = 10000m,
            paidCard = 0m,
            paidBonus = 0m,
            items = new[] { new { variantId = cola.DefaultVariantId, quantity = 1m } },
            discountAmount = 0m
        });
        saleResp.EnsureSuccessStatusCode();

        var customers = await client.GetFromJsonAsync<List<Customer>>("/api/customers?search=Cashback Test Customer");
        var customer = customers!.First(c => c.Id == customerId);
        Assert.Equal(700m, customer.CashbackBalance);
    }
}
