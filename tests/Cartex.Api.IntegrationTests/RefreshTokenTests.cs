using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Api.IntegrationTests;

[Collection("api")]
public class RefreshTokenTests(CartexApiFactory factory)
{
    private sealed record Login(string Token, string RefreshToken);
    private sealed record Session(long Id, string? DeviceName, DateTime CreatedAt, DateTime LastUsedAt, DateTime ExpiresAt);

    private async Task<Login> LoginAsync(HttpClient client, string device = "Test-Device")
    {
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", password = "admin123", deviceName = device });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Login>())!;
    }

    [Fact]
    public async Task Refresh_Rotates_And_ReuseWithinGrace_Rejected_FamilySurvives()
    {
        var client = factory.CreateClient();
        var first = await LoginAsync(client);

        var rotated = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = first.RefreshToken });
        rotated.EnsureSuccessStatusCode();
        var second = (await rotated.Content.ReadFromJsonAsync<Login>())!;
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        Assert.False(string.IsNullOrEmpty(second.Token));

        var reuse = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = first.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

        var stillValid = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = second.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, stillValid.StatusCode);
    }

    [Fact]
    public async Task Refresh_ReuseAfterGrace_RevokesFamily()
    {
        var client = factory.CreateClient();
        var first = await LoginAsync(client, "Grace-Device");

        var rotated = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = first.RefreshToken });
        rotated.EnsureSuccessStatusCode();
        var second = (await rotated.Content.ReadFromJsonAsync<Login>())!;

        await BackdateRevokedSessionsAsync("admin");

        var reuse = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = first.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

        var afterCascade = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = second.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, afterCascade.StatusCode);
    }

    private async Task BackdateRevokedSessionsAsync(string username)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userId = await db.Users.Where(u => u.Username == username).Select(u => u.Id).FirstAsync();
        var cutoff = DateTime.UtcNow.AddMinutes(-2);
        await db.RefreshSessions.Where(s => s.UserId == userId && s.RevokedAt != null && s.RevokedAt > cutoff)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, cutoff));
    }

    [Fact]
    public async Task Logout_Revokes_RefreshToken()
    {
        var client = factory.CreateClient();
        var login = await LoginAsync(client);

        var logout = await client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = login.RefreshToken });
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = login.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithInvalidToken_Is401()
    {
        var client = factory.CreateClient();
        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = "not-a-real-token" });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Sessions_ListsAndRevoke_BlocksRefresh()
    {
        var client = factory.CreateClient();
        var login = await LoginAsync(client, "Sessions-Device");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);

        var sessions = await client.GetFromJsonAsync<List<Session>>("/api/auth/sessions");
        var mine = sessions!.FirstOrDefault(s => s.DeviceName == "Sessions-Device");
        Assert.NotNull(mine);

        var revoke = await client.DeleteAsync($"/api/auth/sessions/{mine!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = login.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task DeactivatedUser_CannotRefresh()
    {
        var client = factory.CreateClient();
        var login = await LoginAsync(client, "Deactivate-Device");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Users.Where(u => u.Username == "admin")
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.IsActive, false));
        }

        try
        {
            var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = login.RefreshToken });
            Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        }
        finally
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Users.Where(u => u.Username == "admin")
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.IsActive, true));
        }
    }
}
