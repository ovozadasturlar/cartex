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
            var operatorClient = await AuthHelper.LoginAsync(
                factory, "developer", "developer123", "device-A", "Kassa 1");
            var warehouses = await operatorClient.GetFromJsonAsync<List<IdName>>("/api/warehouses");
            var warehouseId = warehouses![0].Id;

            var claim1Res = await operatorClient.PostAsJsonAsync("/api/offline-cache/claim",
                new { deviceId = "device-A", deviceName = "Kassa 1", warehouseId });
            claim1Res.EnsureSuccessStatusCode();
            var grantA = await claim1Res.Content.ReadFromJsonAsync<Cartex.Shared.Models.OfflineCache.OfflineLeaseGrantDto>();
            Assert.NotNull(grantA);

            var second = await operatorClient.PostAsJsonAsync("/api/offline-cache/claim",
                new { deviceId = "device-B", deviceName = "Kassa 2", warehouseId });
            Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

            var state = await operatorClient.GetFromJsonAsync<Cartex.Shared.Models.OfflineCache.OfflineCacheStateDto>("/api/offline-cache");
            Assert.Equal("device-A", state!.DeviceId);

            var seller = await AuthHelper.LoginAsync(factory, "seller", "seller123", "device-A", "Kassa 1");
            using var snapshotReq = new HttpRequestMessage(HttpMethod.Get,
                $"/api/offline-cache/snapshot?leaseId={grantA.LeaseId}&epoch={grantA.Epoch}");
            snapshotReq.Headers.Add("X-Offline-Lease-Token", grantA.LeaseToken);
            var snapshotRes = await seller.SendAsync(snapshotReq);
            Assert.True(snapshotRes.StatusCode is HttpStatusCode.OK or HttpStatusCode.Forbidden);

            using var strangerReq = new HttpRequestMessage(HttpMethod.Get,
                $"/api/offline-cache/snapshot?leaseId={grantA.LeaseId}&epoch={grantA.Epoch}");
            strangerReq.Headers.Add("X-Offline-Lease-Token", "invalid-token");
            var strangerRes = await seller.SendAsync(strangerReq);
            Assert.Equal(HttpStatusCode.Forbidden, strangerRes.StatusCode);

            (await operatorClient.PostAsJsonAsync("/api/offline-cache/release",
                new { leaseId = grantA.LeaseId, leaseToken = grantA.LeaseToken, force = false, reason = "cleanup" })).EnsureSuccessStatusCode();

            var claim2Res = await operatorClient.PostAsJsonAsync("/api/offline-cache/claim",
                new { deviceId = "device-B", deviceName = "Kassa 2", warehouseId });
            claim2Res.EnsureSuccessStatusCode();
            var grantB = await claim2Res.Content.ReadFromJsonAsync<Cartex.Shared.Models.OfflineCache.OfflineLeaseGrantDto>();

            var holderB = await AuthHelper.LoginAsync(factory, "developer", "developer123", "device-B", "Kassa 2");
            (await holderB.PostAsJsonAsync("/api/offline-cache/release",
                new { leaseId = grantB!.LeaseId, leaseToken = grantB.LeaseToken, force = false, reason = "cleanup" })).EnsureSuccessStatusCode();
        }
        finally
        {
            (await dev.PutAsJsonAsync("/api/features/offline_cache", new { isEnabled = false })).EnsureSuccessStatusCode();
        }
    }
}
