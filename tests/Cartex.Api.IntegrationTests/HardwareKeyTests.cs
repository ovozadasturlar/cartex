using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Cartex.Api.IntegrationTests;

[Collection("api")]
public class HardwareKeyTests(CartexApiFactory factory)
{
    private sealed record UserDto(long Id, string FullName, string Username);
    private sealed record HardwareKeyResult(string FileName, string Content);
    private sealed record LoginResponse(string Token, string FullName, string Role);

    private async Task<long> SellerIdAsync(HttpClient developer)
    {
        var users = await developer.GetFromJsonAsync<List<UserDto>>("/api/users");
        return users!.Single(u => u.Username == "seller").Id;
    }

    [Fact]
    public async Task IssueKey_ThenLogin_Works_WithMatchingSerial()
    {
        var developer = await AuthHelper.LoginAsync(factory, "developer", "developer123");
        var sellerId = await SellerIdAsync(developer);

        var issue = await developer.PostAsJsonAsync("/api/hardware-keys", new { userId = sellerId, serial = "AB12CD34" });
        issue.EnsureSuccessStatusCode();
        var key = await issue.Content.ReadFromJsonAsync<HardwareKeyResult>();
        Assert.NotNull(key);

        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login-with-key", new { keyContent = key!.Content, serial = "AB12CD34" });
        login.EnsureSuccessStatusCode();
        var body = await login.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.Equal("seller", body!.Role);
    }

    [Fact]
    public async Task Login_Fails_WhenSerialDoesNotMatch()
    {
        var developer = await AuthHelper.LoginAsync(factory, "developer", "developer123");
        var sellerId = await SellerIdAsync(developer);

        var issue = await developer.PostAsJsonAsync("/api/hardware-keys", new { userId = sellerId, serial = "AB12CD34" });
        issue.EnsureSuccessStatusCode();
        var key = await issue.Content.ReadFromJsonAsync<HardwareKeyResult>();

        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login-with-key", new { keyContent = key!.Content, serial = "FF00FF00" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task IssueKey_Is403_ForSeller()
    {
        var seller = await AuthHelper.LoginAsync(factory, "seller", "seller123");
        var response = await seller.PostAsJsonAsync("/api/hardware-keys", new { userId = 1L, serial = "AB12CD34" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
