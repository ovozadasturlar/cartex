using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Cartex.Api.IntegrationTests;

[Collection("api")]
public class CustomerPhoneTests(CartexApiFactory factory)
{
    private static Task<HttpResponseMessage> CreateAsync(HttpClient client, string name, string phone) =>
        client.PostAsJsonAsync("/api/customers", new { fullName = name, phone, cardBarcode = (string?)null, discountPct = 0m });

    [Fact]
    public async Task Phone_IsNormalized_And_DuplicateFormats_Conflict()
    {
        var client = await AuthHelper.LoginAsync(factory, "admin", "admin123");

        var first = await CreateAsync(client, "Normalize Test", "+998 90 777-66-55");
        first.EnsureSuccessStatusCode();

        var duplicate = await CreateAsync(client, "Normalize Dup", "907776655");
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task InvalidPhone_Is400()
    {
        var client = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        var resp = await CreateAsync(client, "Bad Phone", "12-34");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
