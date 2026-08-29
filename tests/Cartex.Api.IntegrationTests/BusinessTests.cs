using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Cartex.Api.IntegrationTests;

[Collection("api")]
public class BusinessTests(CartexApiFactory factory)
{
    private sealed record BusinessDto(string Name, string? LegalName, string Currency, bool IsOnboarded);

    [Fact]
    public async Task GetBusiness_ReturnsSeededOnboardedBusiness()
    {
        var client = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        var business = await client.GetFromJsonAsync<BusinessDto>("/api/business");
        Assert.NotNull(business);
        Assert.True(business.IsOnboarded);
        Assert.False(string.IsNullOrEmpty(business.Currency));
    }

    [Fact]
    public async Task UpdateBusiness_Is403_ForSeller()
    {
        var seller = await AuthHelper.LoginAsync(factory, "seller", "seller123");
        var response = await seller.PutAsJsonAsync("/api/business",
            new { name = "Hacked", legalName = (string?)null, currency = "USD" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateBusiness_Admin_ChangesCurrency()
    {
        var admin = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        var original = await admin.GetFromJsonAsync<BusinessDto>("/api/business");

        var update = await admin.PutAsJsonAsync("/api/business",
            new { name = original!.Name, legalName = original.LegalName, currency = "EUR" });
        update.EnsureSuccessStatusCode();

        var changed = await admin.GetFromJsonAsync<BusinessDto>("/api/business");
        Assert.Equal("EUR", changed!.Currency);

        await admin.PutAsJsonAsync("/api/business",
            new { name = original.Name, legalName = original.LegalName, currency = original.Currency });
    }
}
