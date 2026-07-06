using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Cartex.Api.IntegrationTests;

public static class AuthHelper
{
    private sealed record LoginResponse(string Token, string FullName, string Role);

    public static async Task<HttpClient> LoginAsync(CartexApiFactory factory, string username, string password)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Token);
        return client;
    }

    public static async Task EnsureOpenShiftAsync(HttpClient client)
    {
        var current = await client.GetAsync("/api/shifts/current");
        if (current.StatusCode == System.Net.HttpStatusCode.NoContent)
            (await client.PostAsJsonAsync("/api/shifts/open", new { openingFloat = 0m })).EnsureSuccessStatusCode();
    }

    public static async Task EnsureNoOpenShiftAsync(HttpClient client)
    {
        var current = await client.GetAsync("/api/shifts/current");
        if (current.StatusCode != System.Net.HttpStatusCode.OK) return;
        var shift = await current.Content.ReadFromJsonAsync<CurrentShift>();
        (await client.PostAsJsonAsync($"/api/shifts/{shift!.Id}/close", new { countedCash = 0m })).EnsureSuccessStatusCode();
    }

    private sealed record CurrentShift(long Id);
}
