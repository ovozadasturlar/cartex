using System.Net;
using Xunit;

namespace Cartex.Api.IntegrationTests;

[Collection("api")]
public class ShiftApiTests(CartexApiFactory factory)
{
    [Fact]
    public async Task GetCurrent_ReturnsNoContent_WhenNoOpenShift()
    {
        var admin = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        await AuthHelper.EnsureNoOpenShiftAsync(admin);
        var response = await admin.GetAsync("/api/shifts/current");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
