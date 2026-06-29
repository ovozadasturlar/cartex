using System.Net;
using Xunit;

namespace Cartex.Api.IntegrationTests;

[Collection("api")]
public class AuthorizationTests(CartexApiFactory factory)
{
    [Fact]
    public async Task Anonymous_Is401_OnProtectedEndpoint()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/features");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Seller_Is403_OnDeveloperOnlyEndpoint()
    {
        var client = await AuthHelper.LoginAsync(factory, "seller", "seller123");
        var response = await client.GetAsync("/api/features");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_Is403_OnDeveloperOnlyEndpoint()
    {
        var client = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        var response = await client.GetAsync("/api/features");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Developer_CanAccess_AllDeveloperEndpoints()
    {
        var client = await AuthHelper.LoginAsync(factory, "developer", "developer123");
        Assert.True((await client.GetAsync("/api/features")).IsSuccessStatusCode);
        Assert.True((await client.GetAsync("/api/license")).IsSuccessStatusCode);
        Assert.True((await client.GetAsync("/api/settings")).IsSuccessStatusCode);
    }

    [Fact]
    public async Task Seller_CanAccess_AllowedEndpoints()
    {
        var client = await AuthHelper.LoginAsync(factory, "seller", "seller123");
        Assert.True((await client.GetAsync("/api/products")).IsSuccessStatusCode);
        Assert.True((await client.GetAsync("/api/branches")).IsSuccessStatusCode);
    }
}
