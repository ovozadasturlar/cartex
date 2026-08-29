using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cartex.Shared.Models.Catalog;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Cartex.Api.IntegrationTests;

[Collection("api")]
public class CatalogReferenceTests(CartexApiFactory factory)
{
    private sealed record IdName(long Id, string Name);
    private sealed record ProductRow(long Id, long DefaultVariantId, string Name, decimal? SellingPrice);
    private sealed record VariantRow(long Id, bool IsDefault);
    private sealed record PriceInfo(decimal? LastPurchasePrice, decimal? SellingPrice);
    private sealed record CatalogSettingsRow(CatalogPackDto? Pack, string? LastError);

    private const string PackBarcode = "4790000000021";

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
        var response = await client.GetAsync($"/api/scan?code={code}&warehouseId={warehouseId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    [Fact]
    public async Task MAKAT_01_Reference_scan_does_not_create_a_shop_product() =>
        await WithReferenceAsync(async (client, stub, warehouseId) =>
        {
            const string barcode = "4790000000041";
            const string name = "MAKAT-01 taklif mahsuloti";
            stub.Product = new CatalogProductDto(
                barcode, name, null, "Brend", "Katta", "Kichik", null, "dona", 1m, null);

            var before = await client.GetFromJsonAsync<List<ProductRow>>($"/api/products?search={Uri.EscapeDataString(name)}");
            Assert.Empty(before!);

            var result = await ScanAsync(client, barcode, warehouseId);
            Assert.Equal("reference", result.GetProperty("kind").GetString());

            var after = await client.GetFromJsonAsync<List<ProductRow>>($"/api/products?search={Uri.EscapeDataString(name)}");
            Assert.Empty(after!);

            var byBarcode = await client.GetAsync($"/api/products/by-barcode?code={barcode}&warehouseId={warehouseId}");
            Assert.Equal(HttpStatusCode.NotFound, byBarcode.StatusCode);
        });

    [Fact]
    public async Task MAKAT_03_Existing_barcode_blocks_a_new_product()
    {
        var client = await AuthHelper.LoginAsync(factory, "admin", "admin123");

        await CreateOwnerAsync(client, "Suv", "4780000000001");
        var duplicate = await CreateOwnerAsync(client, "Ikkinchi mahsulot", "4780000000001");

        Assert.False(duplicate.IsSuccessStatusCode);
    }

    [Fact]
    public async Task MAKAT_03_Rejection_names_the_product_that_owns_the_barcode()
    {
        var client = await AuthHelper.LoginAsync(factory, "admin", "admin123");

        await CreateOwnerAsync(client, "Suv egasi", "4780000000002");
        var duplicate = await CreateOwnerAsync(client, "Uchinchi mahsulot", "4780000000002");

        Assert.Contains("Suv egasi", await duplicate.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    private static async Task<HttpResponseMessage> CreateOwnerAsync(HttpClient client, string name, string barcode)
    {
        var units = await client.GetFromJsonAsync<List<IdName>>("/api/units");
        return await client.PostAsJsonAsync("/api/products", new
        {
            name,
            categoryId = (long?)null,
            unitId = units![0].Id,
            minStock = 0m,
            barcodes = new[] { new { code = barcode, packQty = 1m } }
        });
    }

    [Fact]
    public async Task MAKAT_04_Reference_result_carries_no_price() =>
        await WithReferenceAsync(async (client, stub, warehouseId) =>
        {
            const string barcode = "4790000000031";
            stub.Product = new CatalogProductDto(
                barcode, "Narxsiz taklif", null, "Brend", "Katta", "Kichik", "M-1", "dona", 12m, null);

            var result = await ScanAsync(client, barcode, warehouseId);

            Assert.Equal("reference", result.GetProperty("kind").GetString());
            foreach (var field in result.GetProperty("reference").EnumerateObject())
            {
                Assert.DoesNotContain("price", field.Name, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("cost", field.Name, StringComparison.OrdinalIgnoreCase);
            }
        });

    [Fact]
    public async Task MAKAT_04_Product_created_from_reference_has_no_selling_or_cost_price() =>
        await WithReferenceAsync(async (client, stub, warehouseId) =>
        {
            const string barcode = "4790000000032";
            stub.Product = new CatalogProductDto(
                barcode, "Referens mahsulot", null, null, null, null, null, "dona", 1m, null);
            var reference = (await ScanAsync(client, barcode, warehouseId)).GetProperty("reference");

            var units = await client.GetFromJsonAsync<List<IdName>>("/api/units");
            var create = await client.PostAsJsonAsync("/api/products", new
            {
                name = reference.GetProperty("name").GetString(),
                categoryId = (long?)null,
                unitId = units![0].Id,
                minStock = 0m,
                barcodes = new[] { new { code = barcode, packQty = 1m } }
            });
            create.EnsureSuccessStatusCode();
            var productId = await create.Content.ReadFromJsonAsync<long>();

            var product = (await client.GetFromJsonAsync<List<ProductRow>>("/api/products?search=Referens"))!
                .Single(p => p.Id == productId);
            var variantId = (await client.GetFromJsonAsync<List<VariantRow>>($"/api/products/{productId}/variants"))!.Single().Id;
            var price = await client.GetFromJsonAsync<PriceInfo>(
                $"/api/products/variants/{variantId}/price-info?warehouseId={warehouseId}");

            Assert.Null(product.SellingPrice);
            Assert.Null(price!.SellingPrice);
            Assert.Null(price.LastPurchasePrice);
        });

    [Fact]
    public async Task MAKAT_06_Reference_names_create_no_catalog_records() =>
        await WithReferenceAsync(async (client, stub, warehouseId) =>
        {
            const string barcode = "4790000000033";
            const string unit = "ma'lumotnoma birligi";
            const string category = "Ma'lumotnoma bo'limi";
            const string manufacturer = "Ma'lumotnoma ishlab chiqaruvchisi";
            stub.Product = new CatalogProductDto(
                barcode, "Mos yozuvsiz taklif", null, manufacturer, category, null, null, unit, 1m, null);

            var unitsBefore = await client.GetFromJsonAsync<List<IdName>>("/api/units");
            var categoriesBefore = await client.GetFromJsonAsync<List<IdName>>("/api/categories");
            var manufacturersBefore = await client.GetFromJsonAsync<List<IdName>>("/api/manufacturers");

            var reference = (await ScanAsync(client, barcode, warehouseId)).GetProperty("reference");

            var unitsAfter = await client.GetFromJsonAsync<List<IdName>>("/api/units");
            var categoriesAfter = await client.GetFromJsonAsync<List<IdName>>("/api/categories");
            var manufacturersAfter = await client.GetFromJsonAsync<List<IdName>>("/api/manufacturers");

            Assert.Equal(unit, reference.GetProperty("unit").GetString());
            Assert.Equal(category, reference.GetProperty("categoryParent").GetString());
            Assert.Equal(manufacturer, reference.GetProperty("manufacturer").GetString());
            Assert.Equal(unitsBefore!.Count, unitsAfter!.Count);
            Assert.Equal(categoriesBefore!.Count, categoriesAfter!.Count);
            Assert.Equal(manufacturersBefore!.Count, manufacturersAfter!.Count);
            Assert.DoesNotContain(unitsAfter, u => u.Name == unit);
            Assert.DoesNotContain(categoriesAfter, c => c.Name == category);
            Assert.DoesNotContain(manufacturersAfter, m => m.Name == manufacturer);
        });

    [Fact]
    public async Task KLIENT_12_Pack_with_invalid_signature_is_refused()
    {
        var client = await AuthHelper.LoginAsync(factory, "admin", "admin123");

        var response = await UploadUnsignedPackAsync(client);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("catalog_pack_signature_invalid", problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task KLIENT_17_Refused_pack_keeps_stored_pack_and_shows_the_reason() =>
        await WithReferenceAsync(async (client, stub, warehouseId) =>
        {
            var before = await client.GetFromJsonAsync<CatalogSettingsRow>("/api/settings/catalog");

            var upload = await UploadUnsignedPackAsync(client);
            Assert.Equal(HttpStatusCode.BadRequest, upload.StatusCode);

            var after = await client.GetFromJsonAsync<CatalogSettingsRow>("/api/settings/catalog");
            Assert.Equal(before!.Pack, after!.Pack);
            Assert.False(string.IsNullOrWhiteSpace(after.LastError));
            Assert.Contains("imzo", after.LastError!, StringComparison.OrdinalIgnoreCase);

            await CatalogSettings.WriteAsync(factory, CatalogSourceMode.File, stub.Url);
            var result = await ScanAsync(client, PackBarcode, warehouseId);
            Assert.Equal("none", result.GetProperty("kind").GetString());
        });

    private static async Task<HttpResponseMessage> UploadUnsignedPackAsync(HttpClient client)
    {
        var pack = await BuildPackAsync();
        var manifest = JsonSerializer.Serialize(new
        {
            segment = "test",
            version = 1,
            rowCount = 1,
            byteSize = pack.LongLength,
            sha256 = Convert.ToBase64String(SHA256.HashData(pack)),
            signature = Convert.ToBase64String(new byte[64])
        });

        using var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(pack), "pack", "catalog.db" },
            { new ByteArrayContent(Encoding.UTF8.GetBytes(manifest)), "manifest", "catalog.json" }
        };
        return await client.PostAsync("/api/settings/catalog/pack", content);
    }

    private static async Task<byte[]> BuildPackAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".db");
        await using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString()))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                create table products (barcode text not null, name text not null, name_cyrl text,
                    search_fold text not null, manufacturer text, category text, model text,
                    unit text not null, pack_qty real, image_path text);
                create table meta (key text primary key, value text not null);
                insert into products values ('{PackBarcode}', 'Imzosiz paket mahsuloti', null,
                    'imzosiz paket mahsuloti', null, null, null, 'dona', 1, null);
                insert into meta values ('rowCount', '1');
                """;
            await command.ExecuteNonQueryAsync();
        }

        var bytes = await File.ReadAllBytesAsync(path);
        File.Delete(path);
        return bytes;
    }
}
