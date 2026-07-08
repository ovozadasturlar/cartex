using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Cartex.Api.IntegrationTests;

[Collection("api")]
public class OfflineCacheTests(CartexApiFactory factory)
{
    private sealed record IdName(long Id, string Name);
    private sealed record StateDto(string? DeviceId, string? DeviceName, DateTime? ClaimedAt);
    private sealed record SnapshotDto(string BaseCurrency, List<SnapshotProduct> Products, List<SnapshotCustomer> Customers);
    private sealed record SnapshotProduct(long VariantId, string ProductName, decimal Quantity, decimal SellingPrice);
    private sealed record SnapshotCustomer(long Id, string FullName, decimal DebtBalance);

    [Fact]
    public async Task Claim_IsExclusive_PerBusiness_And_Snapshot_RequiresHolder()
    {
        var dev = await AuthHelper.LoginAsync(factory, "developer", "developer123");
        (await dev.PutAsJsonAsync("/api/features/offline_cache", new { isEnabled = true })).EnsureSuccessStatusCode();
        try
        {
            var admin = await AuthHelper.LoginAsync(factory, "admin", "admin123");

            (await admin.PostAsJsonAsync("/api/offline-cache/claim",
                new { deviceId = "device-A", deviceName = "Kassa 1" })).EnsureSuccessStatusCode();

            (await admin.PostAsJsonAsync("/api/offline-cache/claim",
                new { deviceId = "device-A", deviceName = "Kassa 1" })).EnsureSuccessStatusCode();

            var second = await admin.PostAsJsonAsync("/api/offline-cache/claim",
                new { deviceId = "device-B", deviceName = "Kassa 2" });
            Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);

            var state = await admin.GetFromJsonAsync<StateDto>("/api/offline-cache");
            Assert.Equal("device-A", state!.DeviceId);

            var warehouses = await admin.GetFromJsonAsync<List<IdName>>("/api/warehouses");
            var snapshot = await admin.GetFromJsonAsync<SnapshotDto>(
                $"/api/offline-cache/snapshot?warehouseId={warehouses![0].Id}&deviceId=device-A");
            Assert.NotEmpty(snapshot!.Products);

            var stranger = await admin.GetAsync(
                $"/api/offline-cache/snapshot?warehouseId={warehouses[0].Id}&deviceId=device-B");
            Assert.Equal(HttpStatusCode.BadRequest, stranger.StatusCode);

            (await admin.PostAsync("/api/offline-cache/release", null)).EnsureSuccessStatusCode();
            (await admin.PostAsJsonAsync("/api/offline-cache/claim",
                new { deviceId = "device-B", deviceName = "Kassa 2" })).EnsureSuccessStatusCode();
            (await admin.PostAsync("/api/offline-cache/release", null)).EnsureSuccessStatusCode();
        }
        finally
        {
            (await dev.PutAsJsonAsync("/api/features/offline_cache", new { isEnabled = false })).EnsureSuccessStatusCode();
        }
    }
}
