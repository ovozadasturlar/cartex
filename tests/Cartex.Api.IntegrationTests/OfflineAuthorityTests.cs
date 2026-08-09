using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cartex.Shared.Models.OfflineCache;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Api.IntegrationTests;

[Collection("api")]
public sealed class OfflineAuthorityTests(CartexApiFactory factory)
{
    private sealed record IdName(long Id, string Name);
    private sealed record Product(long Id, long DefaultVariantId, string Name, decimal? SellingPrice);
    private sealed record StockRow(long VariantId, string ProductName, decimal Quantity);
    private sealed record StockPage(List<StockRow> Items, int TotalCount);

    [Fact]
    public async Task Claim_is_atomic_and_replayed_event_is_applied_exactly_once()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var first = await AuthHelper.LoginAsync(factory, "developer", "developer123",
            $"offline-a-{suffix}", "Offline A");
        var second = await AuthHelper.LoginAsync(factory, "developer", "developer123",
            $"offline-b-{suffix}", "Offline B");
        (await first.PutAsJsonAsync("/api/features/offline_cache", new { isEnabled = true }))
            .EnsureSuccessStatusCode();

        var existing = await first.GetFromJsonAsync<OfflineCacheStateDto>("/api/offline-cache");
        if (existing?.LeaseId is not null)
        {
            var release = await first.PostAsJsonAsync("/api/offline-cache/release",
                new ReleaseOfflineCacheRequest(existing.LeaseId, Force: true, Reason: "integration reset"));
            release.EnsureSuccessStatusCode();
        }

        var warehouse = (await first.GetFromJsonAsync<List<IdName>>("/api/warehouses"))!.First();
        var claimA = first.PostAsJsonAsync("/api/offline-cache/claim",
            new ClaimOfflineCacheRequest($"offline-a-{suffix}", "Offline A", warehouse.Id));
        var claimB = second.PostAsJsonAsync("/api/offline-cache/claim",
            new ClaimOfflineCacheRequest($"offline-b-{suffix}", "Offline B", warehouse.Id));
        var responses = await Task.WhenAll(claimA, claimB);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);

        var winnerIndex = responses[0].IsSuccessStatusCode ? 0 : 1;
        var winner = winnerIndex == 0 ? first : second;
        var grant = await responses[winnerIndex].Content.ReadFromJsonAsync<OfflineLeaseGrantDto>();
        Assert.NotNull(grant);

        await AuthHelper.EnsureOpenShiftAsync(winner);
        var onHand = await winner.GetFromJsonAsync<StockPage>(
            $"/api/stocks/on-hand?warehouseId={warehouse.Id}&forSale=true&page=1&pageSize=200");
        var stocked = Assert.Single(onHand!.Items.Where(x => x.Quantity > 0).Take(1));
        var products = await winner.GetFromJsonAsync<List<Product>>("/api/products");
        var product = products!.First(p => p.DefaultVariantId == stocked.VariantId);
        var paidCash = Math.Max(1m, product.SellingPrice ?? 1m);
        var payload = JsonSerializer.SerializeToElement(new
        {
            warehouseId = warehouse.Id,
            customerId = (long?)null,
            paidCash,
            paidCard = 0m,
            paidBonus = 0m,
            items = new[] { new { variantId = product.DefaultVariantId, quantity = 1m } },
            discountAmount = 0m,
            applyAutoDiscount = false
        });
        var offlineEvent = new OfflineSyncEventRequest(
            Guid.NewGuid(), 1, "sale.create", $"offline-sale-{suffix}", DateTime.UtcNow, payload);
        var batch = new OfflineSyncBatchRequest(grant!.LeaseId, grant.Epoch, grant.LeaseToken, [offlineEvent]);

        var firstPush = await winner.PostAsJsonAsync("/api/offline-cache/sync/batches", batch);
        firstPush.EnsureSuccessStatusCode();
        var firstResult = await firstPush.Content.ReadFromJsonAsync<OfflineSyncBatchResult>();
        var applied = Assert.Single(firstResult!.Results);
        Assert.Equal("Applied", applied.Status);
        Assert.NotNull(applied.ResultEntityId);

        var retryPush = await winner.PostAsJsonAsync("/api/offline-cache/sync/batches", batch);
        retryPush.EnsureSuccessStatusCode();
        var retryResult = await retryPush.Content.ReadFromJsonAsync<OfflineSyncBatchResult>();
        var replayed = Assert.Single(retryResult!.Results);
        Assert.Equal("AlreadyApplied", replayed.Status);
        Assert.Equal(applied.ResultEntityId, replayed.ResultEntityId);

        var sale = await winner.GetAsync($"/api/sales/{applied.ResultEntityId}");
        sale.EnsureSuccessStatusCode();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.OfflineAuthorityLeases.Where(x => x.Id == grant.LeaseId)
                .ExecuteUpdateAsync(x => x.SetProperty(
                    row => row.LastHeartbeatAt, DateTime.UtcNow.AddMinutes(-2)));
        }

        var nonHolder = winnerIndex == 0 ? second : first;
        await AuthHelper.EnsureOpenShiftAsync(nonHolder);
        var splitBrainSale = await nonHolder.PostAsJsonAsync("/api/sales", new
        {
            warehouseId = warehouse.Id,
            customerId = (long?)null,
            paidCash,
            paidCard = 0m,
            paidBonus = 0m,
            items = new[] { new { variantId = product.DefaultVariantId, quantity = 1m } },
            applyAutoDiscount = false,
            idempotencyKey = $"split-brain-{suffix}"
        });
        Assert.Equal(HttpStatusCode.Conflict, splitBrainSale.StatusCode);

        (await nonHolder.PostAsJsonAsync("/api/offline-cache/release",
            new ReleaseOfflineCacheRequest(grant.LeaseId, Force: true, Reason: "integration force release")))
            .EnsureSuccessStatusCode();
        var staleHeartbeat = await winner.PostAsJsonAsync("/api/offline-cache/heartbeat",
            new OfflineHeartbeatRequest(grant.LeaseId, grant.Epoch, grant.LeaseToken));
        Assert.Equal(HttpStatusCode.Conflict, staleHeartbeat.StatusCode);
    }
}
