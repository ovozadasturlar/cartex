using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cartex.ApiClient;
using Cartex.ApiClient.Api;
using Refit;
using Xunit;

namespace Cartex.Api.IntegrationTests;

[Collection("api")]
public class ShiftRefitRepro(CartexApiFactory factory)
{
    private sealed record LoginResponse(string Token);

    [Fact]
    public async Task GetCurrent_ViaRefit_ReturnsNull_WhenNoShift()
    {
        var cleanup = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        await AuthHelper.EnsureNoOpenShiftAsync(cleanup);

        var auth = factory.CreateClient();
        var login = await auth.PostAsJsonAsync("/api/auth/login", new { username = "admin", password = "admin123" });
        var token = (await login.Content.ReadFromJsonAsync<LoginResponse>())!.Token;

        var handler = new NoContentHandler { InnerHandler = factory.Server.CreateHandler() };
        var client = new HttpClient(handler) { BaseAddress = factory.Server.BaseAddress };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var api = RestService.For<IShiftsApi>(client);
        var result = await api.GetCurrentAsync();
        Assert.Null(result);
    }
}
