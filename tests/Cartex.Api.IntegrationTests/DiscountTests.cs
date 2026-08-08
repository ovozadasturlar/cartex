using System.Net.Http.Json;
using Xunit;

namespace Cartex.Api.IntegrationTests;

[Collection("api")]
public class DiscountTests(CartexApiFactory factory)
{
    private sealed record IdName(long Id, string Name);
    private sealed record Lookup(long VariantId, string ProductName, string UnitName, decimal PackQty, decimal SellingPrice, decimal OnHand);
    private sealed record Receipt(string ReceiptToken, decimal TotalAmount, decimal DiscountAmount, decimal PaidCash, decimal DebtAmount);
    private sealed record SaleResult(long SaleId, string ReceiptToken);

    [Fact]
    public async Task Sale_WithDiscount_PersistsNetTotalAndDiscount()
    {
        var client = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        await AuthHelper.EnsureOpenShiftAsync(client);
        var warehouses = await client.GetFromJsonAsync<List<IdName>>("/api/warehouses");
        var warehouseId = warehouses![0].Id;

        var lookup = await client.GetFromJsonAsync<Lookup>(
            $"/api/products/by-barcode?code=5449000214911&warehouseId={warehouseId}");
        var gross = lookup!.SellingPrice * 2;
        const decimal discount = 3000m;
        var net = gross - discount;

        var response = await client.PostAsJsonAsync("/api/sales", new
        {
            warehouseId,
            customerId = (long?)null,
            paidCash = net,
            paidCard = 0m,
            paidBonus = 0m,
            items = new[] { new { variantId = lookup.VariantId, quantity = 2m } },
            discountAmount = discount
        });
        response.EnsureSuccessStatusCode();
        var saleResult = await response.Content.ReadFromJsonAsync<SaleResult>();

        var receipt = await client.GetFromJsonAsync<Receipt>($"/r/{saleResult!.ReceiptToken}");
        Assert.NotNull(receipt);
        Assert.Equal(discount, receipt.DiscountAmount);
        Assert.Equal(net, receipt.TotalAmount);
        Assert.Equal(0m, receipt.DebtAmount);
    }
}
