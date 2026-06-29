using Xunit;

namespace Cartex.Api.IntegrationTests;

[Collection("api")]
public class DeveloperVisibilityTests(CartexApiFactory factory)
{
    [Fact]
    public async Task Admin_DoesNotSee_DeveloperUser()
    {
        var client = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        var body = await client.GetStringAsync("/api/users");
        Assert.DoesNotContain("\"developer\"", body);
    }

    [Fact]
    public async Task Admin_DoesNotSee_DeveloperRole()
    {
        var client = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        var body = await client.GetStringAsync("/api/roles");
        Assert.DoesNotContain("\"developer\"", body);
    }
}
