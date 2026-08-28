using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cartex.Shared.Models.Catalog;
using Xunit;

namespace Cartex.Api.IntegrationTests;

[Collection("api")]
public class ScanResolutionTests(CartexApiFactory factory)
{
    private sealed record IdName(long Id, string Name);
    private sealed record Product(long Id, long DefaultVariantId, string Name);
    private sealed record Label(long Id, string LabelCode);

    private const string ReferenceOnlyBarcode = "4790000000013";
    private const string NowhereBarcode = "4790000000099";

    private async Task WithReferenceAsync(Func<HttpClient, CatalogStub, long, Task> body)
    {
        var client = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        var warehouses = await client.GetFromJsonAsync<List<IdName>>("/api/warehouses");
        await using var stub = await CatalogStub.StartAsync();
        var previous = await CatalogSettings.ReadAsync(factory);
        await CatalogSettings.WriteAsync(factory, CatalogSourceMode.Online, stub.Url);
        try
        {
            await body(client, stub, warehouses![0].Id);
        }
        finally
        {
            await CatalogSettings.RestoreAsync(factory, previous);
        }
    }

    private static async Task<JsonElement> ScanAsync(HttpClient client, string code, long warehouseId)
    {
        var response = await client.GetAsync($"/api/scan?code={Uri.EscapeDataString(code)}&warehouseId={warehouseId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private static async Task CreateProductAsync(HttpClient client, string name, string barcode)
    {
        var units = await client.GetFromJsonAsync<List<IdName>>("/api/units");
        var create = await client.PostAsJsonAsync("/api/products", new
        {
            name,
            categoryId = (long?)null,
            unitId = units![0].Id,
            minStock = 0m,
            barcodes = new[] { new { code = barcode, packQty = 1m } }
        });
        create.EnsureSuccessStatusCode();
    }

    private static async Task SellAsync(HttpClient client, long warehouseId)
    {
        await AuthHelper.EnsureOpenShiftAsync(client);
        var products = await client.GetFromJsonAsync<List<Product>>("/api/products?search=Mufta");
        var mufta = products!.First(p => p.Name == "Mufta PPR 20mm");

        var sale = await client.PostAsJsonAsync("/api/sales", new
        {
            warehouseId,
            customerId = (long?)null,
            paidCash = 1500m,
            paidCard = 0m,
            paidBonus = 0m,
            items = new[] { new { variantId = mufta.DefaultVariantId, quantity = 1m } },
            discountAmount = 0m
        });
        sale.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task MAKAT_02_Shop_barcode_resolves_to_product_and_reference_is_not_consulted() =>
        await WithReferenceAsync(async (client, stub, warehouseId) =>
        {
            const string barcode = "4790000000011";
            await CreateProductAsync(client, "Skan tartibi mahsuloti A", barcode);

            var result = await ScanAsync(client, barcode, warehouseId);

            Assert.Equal("product", result.GetProperty("kind").GetString());
            Assert.Equal(0, stub.Requests);
        });

    [Fact]
    public async Task MAKAT_02_Shop_product_wins_when_barcode_is_in_shop_and_reference() =>
        await WithReferenceAsync(async (client, stub, warehouseId) =>
        {
            const string barcode = "4790000000012";
            await CreateProductAsync(client, "Suv do'kon nusxasi", barcode);
            stub.Product = new CatalogProductDto(
                barcode, "Suv ma'lumotnoma nusxasi", null, null, null, null, null, "dona", 1m, null);

            var result = await ScanAsync(client, barcode, warehouseId);

            Assert.Equal("product", result.GetProperty("kind").GetString());
            Assert.Equal("Suv do'kon nusxasi", result.GetProperty("product").GetProperty("productName").GetString());
            Assert.Equal(0, stub.Requests);
        });

    [Fact]
    public async Task MAKAT_02_Unknown_barcode_falls_through_to_reference() =>
        await WithReferenceAsync(async (client, stub, warehouseId) =>
        {
            stub.Product = new CatalogProductDto(
                ReferenceOnlyBarcode, "Ma'lumotnoma mahsuloti", null, "Brend", "Katta", "Kichik", null, "dona", 6m, null);

            var result = await ScanAsync(client, ReferenceOnlyBarcode, warehouseId);

            Assert.Equal("reference", result.GetProperty("kind").GetString());
            Assert.Equal("Ma'lumotnoma mahsuloti", result.GetProperty("reference").GetProperty("name").GetString());
            Assert.Equal(1, stub.Requests);
        });

    [Fact]
    public async Task MAKAT_02_Prepack_code_resolves_to_prepack() =>
        await WithReferenceAsync(async (client, stub, warehouseId) =>
        {
            var products = await client.GetFromJsonAsync<List<Product>>("/api/products?search=PPR truba 25mm");
            var pipe = products!.First(p => p.Name == "PPR truba 25mm PN20");
            var create = await client.PostAsJsonAsync("/api/prepacks",
                new { warehouseId, variantId = pipe.DefaultVariantId, quantity = 1.5m, count = 1 });
            create.EnsureSuccessStatusCode();
            var label = (await create.Content.ReadFromJsonAsync<List<Label>>())!.Single();
            Assert.StartsWith("PP", label.LabelCode);

            var result = await ScanAsync(client, label.LabelCode, warehouseId);

            Assert.Equal("prepack", result.GetProperty("kind").GetString());
            Assert.Equal(0, stub.Requests);
        });

    [Fact]
    public async Task MAKAT_02_Customer_card_resolves_to_customer() =>
        await WithReferenceAsync(async (client, stub, warehouseId) =>
        {
            const string card = "CARDSCAN0001";
            var create = await client.PostAsJsonAsync("/api/customers",
                new { fullName = "Skan karta mijozi", phone = "+998905550001", cardBarcode = card, discountPct = 0m });
            create.EnsureSuccessStatusCode();

            var result = await ScanAsync(client, card, warehouseId);

            Assert.Equal("customer", result.GetProperty("kind").GetString());
            Assert.Equal(0, stub.Requests);
        });

    [Fact]
    public async Task MAKAT_02_Cart_code_resolves_to_cart() =>
        await WithReferenceAsync(async (client, stub, warehouseId) =>
        {
            var developer = await AuthHelper.LoginAsync(factory, "developer", "developer123");
            (await developer.PutAsJsonAsync("/api/features/ordering", new { isEnabled = true })).EnsureSuccessStatusCode();
            (await developer.PutAsJsonAsync("/api/features/modules/ordering", new { isEnabled = true })).EnsureSuccessStatusCode();

            var products = await developer.GetFromJsonAsync<List<Product>>("/api/products?search=Smesitel oshxona");
            var mixer = products!.First(p => p.Name == "Smesitel oshxona Zegor");
            var submit = await developer.PostAsJsonAsync("/api/ordering/carts", new
            {
                warehouseId,
                customerId = (long?)null,
                items = new[] { new { variantId = mixer.DefaultVariantId, quantity = 1m } }
            });
            submit.EnsureSuccessStatusCode();
            var code = (await submit.Content.ReadAsStringAsync()).Trim('"');
            Assert.Equal(32, code.Length);
            Assert.True(code.All(Uri.IsHexDigit));

            var result = await ScanAsync(client, code, warehouseId);

            Assert.Equal("cart", result.GetProperty("kind").GetString());
            Assert.Equal(0, stub.Requests);
        });

    [Fact]
    public async Task KLIENT_02_Unknown_code_is_none_and_never_404() =>
        await WithReferenceAsync(async (client, stub, warehouseId) =>
        {
            var response = await client.GetAsync($"/api/scan?code={NowhereBarcode}&warehouseId={warehouseId}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("none", document.RootElement.GetProperty("kind").GetString());
            Assert.Equal(1, stub.Requests);
        });

    [Fact]
    public async Task MAKAT_05_Unreachable_endpoint_returns_none_and_selling_continues() =>
        await WithReferenceAsync(async (client, stub, warehouseId) =>
        {
            await CatalogSettings.WriteAsync(factory, CatalogSourceMode.Online, "http://127.0.0.1:1/");

            var result = await ScanAsync(client, NowhereBarcode, warehouseId);

            Assert.Equal("none", result.GetProperty("kind").GetString());
            Assert.Equal(0, stub.Requests);
            await SellAsync(client, warehouseId);
        });

    [Fact]
    public async Task MAKAT_05_File_mode_without_pack_returns_none() =>
        await WithReferenceAsync(async (client, stub, warehouseId) =>
        {
            (await client.DeleteAsync("/api/settings/catalog/pack")).EnsureSuccessStatusCode();
            await CatalogSettings.WriteAsync(factory, CatalogSourceMode.File, stub.Url);

            var result = await ScanAsync(client, NowhereBarcode, warehouseId);

            Assert.Equal("none", result.GetProperty("kind").GetString());
            Assert.Equal(0, stub.Requests);
        });

    [Fact]
    public async Task KLIENT_14_Reference_off_still_scans_and_sells() =>
        await WithReferenceAsync(async (client, stub, warehouseId) =>
        {
            const string barcode = "4790000000014";
            await CreateProductAsync(client, "Skan tartibi mahsuloti B", barcode);
            await CatalogSettings.WriteAsync(factory, CatalogSourceMode.Off, stub.Url);
            stub.Product = new CatalogProductDto(
                NowhereBarcode, "Ko'rinmasligi kerak", null, null, null, null, null, "dona", 1m, null);

            var known = await ScanAsync(client, barcode, warehouseId);
            var unknown = await ScanAsync(client, NowhereBarcode, warehouseId);

            Assert.Equal("product", known.GetProperty("kind").GetString());
            Assert.Equal("none", unknown.GetProperty("kind").GetString());
            Assert.Equal(0, stub.Requests);
            await SellAsync(client, warehouseId);
        });

    [Fact]
    public async Task KLIENT_03_By_barcode_endpoint_is_unchanged() =>
        await WithReferenceAsync(async (client, stub, warehouseId) =>
        {
            const string barcode = "4790000000015";
            await CreateProductAsync(client, "Skan tartibi mahsuloti C", barcode);
            stub.Product = new CatalogProductDto(
                ReferenceOnlyBarcode, "Ma'lumotnoma mahsuloti", null, null, null, null, null, "dona", 1m, null);

            var known = await client.GetAsync($"/api/products/by-barcode?code={barcode}&warehouseId={warehouseId}");
            var unknown = await client.GetAsync($"/api/products/by-barcode?code={ReferenceOnlyBarcode}&warehouseId={warehouseId}");

            Assert.Equal(HttpStatusCode.OK, known.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
            Assert.Equal(0, stub.Requests);
        });
}
