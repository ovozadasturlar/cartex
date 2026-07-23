using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cartex.Auth.Services;
using Cartex.Domain.Entities;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Api.IntegrationTests;

[Collection("api")]
public class StoreAuthTests(CartexApiFactory factory)
{
    private sealed record StoreLogin(string Token, string RefreshToken, string FullName);
    private sealed record Profile(long Id, string FullName, string? Phone);

    private async Task<long> SetupCustomerAsync(string phone)
    {
        var dev = await AuthHelper.LoginAsync(factory, "developer", "developer123");
        (await dev.PutAsJsonAsync("/api/features/ordering", new { isEnabled = true })).EnsureSuccessStatusCode();

        var admin = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        var resp = await admin.PostAsJsonAsync("/api/customers",
            new { fullName = "Store Test " + phone, phone, cardBarcode = (string?)null, discountPct = 0 });
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<long>();
    }

    private async Task SeedChallengeAsync(long customerId, string code)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        db.OtpChallenges.Add(new OtpChallenge
        {
            CustomerId = customerId,
            CodeHash = hasher.Hash(code),
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        });
        await db.SaveChangesAsync();
    }

    private async Task<StoreLogin> VerifyAsync(HttpClient client, string phone, string code)
    {
        var resp = await client.PostAsJsonAsync("/api/store/auth/verify", new { phone, code, deviceName = "Store-Test" });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<StoreLogin>())!;
    }

    [Fact]
    public async Task OtpVerify_IssuesCustomerToken_AndMeWorks()
    {
        var phone = "+998907777001";
        var customerId = await SetupCustomerAsync(phone);
        await SeedChallengeAsync(customerId, "111222");

        var client = factory.CreateClient();
        var login = await VerifyAsync(client, phone, "111222");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
        var me = await client.GetFromJsonAsync<Profile>("/api/store/me");
        Assert.Equal(customerId, me!.Id);
        Assert.Equal(phone, me.Phone);
    }

    [Fact]
    public async Task WrongCode_Rejected_AndAttemptsExhausted()
    {
        var phone = "+998907777002";
        var customerId = await SetupCustomerAsync(phone);
        await SeedChallengeAsync(customerId, "333444");

        var client = factory.CreateClient();
        for (var i = 0; i < 5; i++)
        {
            var bad = await client.PostAsJsonAsync("/api/store/auth/verify", new { phone, code = "000000" });
            Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        }

        var exhausted = await client.PostAsJsonAsync("/api/store/auth/verify", new { phone, code = "333444" });
        Assert.Equal(HttpStatusCode.Unauthorized, exhausted.StatusCode);
    }

    [Fact]
    public async Task StoreRefresh_Rotates_ReuseWithinGrace_Rejected_FamilySurvives()
    {
        var phone = "+998907777003";
        var customerId = await SetupCustomerAsync(phone);
        await SeedChallengeAsync(customerId, "555666");

        var client = factory.CreateClient();
        var login = await VerifyAsync(client, phone, "555666");

        var rotated = await client.PostAsJsonAsync("/api/store/auth/refresh", new { refreshToken = login.RefreshToken });
        rotated.EnsureSuccessStatusCode();
        var second = (await rotated.Content.ReadFromJsonAsync<StoreLogin>())!;
        Assert.NotEqual(login.RefreshToken, second.RefreshToken);

        var reuse = await client.PostAsJsonAsync("/api/store/auth/refresh", new { refreshToken = login.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

        var stillValid = await client.PostAsJsonAsync("/api/store/auth/refresh", new { refreshToken = second.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, stillValid.StatusCode);
    }

    [Fact]
    public async Task StoreRefresh_ReuseAfterGrace_RevokesFamily()
    {
        var phone = "+998907777013";
        var customerId = await SetupCustomerAsync(phone);
        await SeedChallengeAsync(customerId, "556677");

        var client = factory.CreateClient();
        var login = await VerifyAsync(client, phone, "556677");

        var rotated = await client.PostAsJsonAsync("/api/store/auth/refresh", new { refreshToken = login.RefreshToken });
        rotated.EnsureSuccessStatusCode();
        var second = (await rotated.Content.ReadFromJsonAsync<StoreLogin>())!;

        await BackdateRevokedSessionsAsync(customerId);

        var reuse = await client.PostAsJsonAsync("/api/store/auth/refresh", new { refreshToken = login.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

        var cascade = await client.PostAsJsonAsync("/api/store/auth/refresh", new { refreshToken = second.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, cascade.StatusCode);
    }

    private async Task BackdateRevokedSessionsAsync(long customerId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var cutoff = DateTime.UtcNow.AddMinutes(-2);
        await db.CustomerSessions.Where(s => s.CustomerId == customerId && s.RevokedAt != null && s.RevokedAt > cutoff)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, cutoff));
    }

    [Fact]
    public async Task CustomerToken_Is401_OnStaffEndpoint()
    {
        var phone = "+998907777004";
        var customerId = await SetupCustomerAsync(phone);
        await SeedChallengeAsync(customerId, "777888");

        var client = factory.CreateClient();
        var login = await VerifyAsync(client, phone, "777888");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
        var staff = await client.GetAsync("/api/products");
        Assert.Equal(HttpStatusCode.Unauthorized, staff.StatusCode);
    }

    [Fact]
    public async Task StaffToken_Is401_OnStoreEndpoint()
    {
        await SetupCustomerAsync("+998907777005");
        var admin = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        var me = await admin.GetAsync("/api/store/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task RequestOtp_IsUniform200_ForKnownAndUnknownPhones()
    {
        await SetupCustomerAsync("+998907777006");

        var client = factory.CreateClient();
        var known = await client.PostAsJsonAsync("/api/store/auth/request-otp", new { phone = "+998907777006" });
        var unknown = await client.PostAsJsonAsync("/api/store/auth/request-otp", new { phone = "+998900000000" });

        Assert.Equal(HttpStatusCode.OK, known.StatusCode);
        Assert.Equal(HttpStatusCode.OK, unknown.StatusCode);
    }
}
