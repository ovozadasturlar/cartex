using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Cartex.Api.IntegrationTests;

[Collection("api")]
public class IntegrationTestMessageTests(CartexApiFactory factory)
{
    [Fact]
    public async Task Test_message_returns_400_when_channel_not_configured()
    {
        var developer = await AuthHelper.LoginAsync(factory, "developer", "developer123");
        var response = await developer.PostAsJsonAsync("/api/settings/integrations/test", new { channel = "telegram", recipient = "123" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Test_message_returns_400_for_unknown_channel()
    {
        var developer = await AuthHelper.LoginAsync(factory, "developer", "developer123");
        var response = await developer.PostAsJsonAsync("/api/settings/integrations/test", new { channel = "pigeon", recipient = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Test_message_requires_settings_manage()
    {
        var seller = await AuthHelper.LoginAsync(factory, "seller", "seller123");
        var response = await seller.PostAsJsonAsync("/api/settings/integrations/test", new { channel = "telegram", recipient = "123" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
