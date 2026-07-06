using System.Net.Http.Json;
using Xunit;

namespace Cartex.Api.IntegrationTests;

[Collection("api")]
public class ReceiptDeliveryTests(CartexApiFactory factory)
{
    private sealed record IdName(long Id, string Name);
    private sealed record Product(long Id, long DefaultVariantId, string Name);
    private sealed record SaleResult(long SaleId, string ReceiptToken);

    private static async Task<string> CreateSaleAsync(HttpClient client)
    {
        var warehouses = await client.GetFromJsonAsync<List<IdName>>("/api/warehouses");
        var products = await client.GetFromJsonAsync<List<Product>>("/api/products");
        var cola = products!.First(p => p.Name == "Coca-Cola 1.5L");
        var resp = await client.PostAsJsonAsync("/api/sales", new
        {
            warehouseId = warehouses![0].Id,
            customerId = (long?)null,
            paidCash = 100_000m,
            paidCard = 0m,
            paidBonus = 0m,
            items = new[] { new { variantId = cola.DefaultVariantId, quantity = 1m } },
            discountAmount = 0m
        });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<SaleResult>())!.ReceiptToken;
    }

    [Fact]
    public async Task Receipt_returns_html_for_browsers_and_json_for_clients()
    {
        var admin = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        await AuthHelper.EnsureOpenShiftAsync(admin);
        var token = await CreateSaleAsync(admin);

        var anonymous = factory.CreateClient();

        var jsonResponse = await anonymous.GetAsync($"/r/{token}");
        jsonResponse.EnsureSuccessStatusCode();
        Assert.Contains("application/json", jsonResponse.Content.Headers.ContentType!.ToString());

        var htmlRequest = new HttpRequestMessage(HttpMethod.Get, $"/r/{token}");
        htmlRequest.Headers.Accept.ParseAdd("text/html");
        var htmlResponse = await anonymous.SendAsync(htmlRequest);
        htmlResponse.EnsureSuccessStatusCode();
        Assert.Contains("text/html", htmlResponse.Content.Headers.ContentType!.ToString());
        var html = await htmlResponse.Content.ReadAsStringAsync();
        Assert.Contains("JAMI", html);
    }

    [Fact]
    public async Task Receipt_pdf_endpoint_returns_pdf()
    {
        var admin = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        await AuthHelper.EnsureOpenShiftAsync(admin);
        var token = await CreateSaleAsync(admin);

        var anonymous = factory.CreateClient();
        var response = await anonymous.GetAsync($"/r/{token}/pdf");
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/pdf", response.Content.Headers.ContentType!.MediaType);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes[..4]));
    }
}
