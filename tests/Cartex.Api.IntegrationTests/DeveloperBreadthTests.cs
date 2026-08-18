using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Cartex.Api.IntegrationTests;

[Collection("api")]
public class DeveloperBreadthTests(CartexApiFactory factory)
{
    private sealed record RoleDto(long Id, string Name, string? Description, string? StartPage, int Priority, bool IsSystem, List<string> Permissions);

    [Fact]
    public async Task Admin_CannotManage_AdminRole()
    {
        var admin = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        var roles = await admin.GetFromJsonAsync<List<RoleDto>>("/api/roles");
        var adminRole = roles!.First(r => r.Name == "admin");

        var response = await admin.PutAsJsonAsync($"/api/roles/{adminRole.Id}/permissions",
            new { roleId = adminRole.Id, permissionIds = new List<long>() });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Developer_CanManage_AdminRole()
    {
        var developer = await AuthHelper.LoginAsync(factory, "developer", "developer123");
        var roles = await developer.GetFromJsonAsync<List<RoleDto>>("/api/roles");
        var adminRole = roles!.First(r => r.Name == "admin");

        var response = await developer.PutAsJsonAsync($"/api/roles/{adminRole.Id}",
            new { id = adminRole.Id, name = adminRole.Name, description = adminRole.Description, startPage = adminRole.StartPage, priority = adminRole.Priority });

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task DisabledFeature_Blocks_Everyone_Even_Wildcard()
    {
        // SOZ-15: feature tekshiruvi hech kimni istisno qilmaydi — wildcard (developer) ham bo'ysunadi;
        // qutqaruv yo'li — feature boshqaruv endpoint'lari feature bilan qulflanmaydi.
        var developer = await AuthHelper.LoginAsync(factory, "developer", "developer123");
        var admin = await AuthHelper.LoginAsync(factory, "admin", "admin123");

        try
        {
            (await developer.PutAsJsonAsync("/api/features/ordering", new { code = "ordering", isEnabled = false }))
                .EnsureSuccessStatusCode();
            (await developer.PutAsJsonAsync("/api/features/store", new { code = "store", isEnabled = false }))
                .EnsureSuccessStatusCode();

            var adminResp = await admin.GetAsync("/api/ordering/carts/none");
            Assert.Equal(HttpStatusCode.Forbidden, adminResp.StatusCode);

            var devResp = await developer.GetAsync("/api/ordering/carts/none");
            Assert.Equal(HttpStatusCode.Forbidden, devResp.StatusCode);

            (await developer.GetAsync("/api/features")).EnsureSuccessStatusCode();
        }
        finally
        {
            (await developer.PutAsJsonAsync("/api/features/ordering", new { code = "ordering", isEnabled = true }))
                .EnsureSuccessStatusCode();
            (await developer.PutAsJsonAsync("/api/features/store", new { code = "store", isEnabled = true }))
                .EnsureSuccessStatusCode();
        }
    }
}
