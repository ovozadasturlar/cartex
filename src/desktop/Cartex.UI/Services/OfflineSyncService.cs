using System.ComponentModel;
using System.Text.Json;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Customers;
using Cartex.Shared.Models.Sales;
using Refit;

namespace Cartex.UI.Services;

public record OfflineSaleItemDraft(long VariantId, decimal Quantity, decimal? UnitPrice);

public record OfflineSaleDraft(long WarehouseId, long? CustomerId, decimal PaidCash, decimal PaidCard, List<OfflineSaleItemDraft> Items, decimal DiscountAmount);

public record OfflineRepayDraft(long CustomerId, decimal Amount);

public sealed class OfflineSyncService(IOfflineCacheApi offlineApi, ISalesApi salesApi, ICustomersApi customersApi, OfflineStore store, ConnectivityService connectivity)
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _hooked;

    public event Action? StateChanged;

    public bool IsEnabled => SettingsService.Instance.OfflineCacheEnabled;

    public void Start()
    {
        if (_hooked) return;
        _hooked = true;
        connectivity.PropertyChanged += OnConnectivityChanged;
        if (IsEnabled)
            _ = Task.Run(SyncAsync);
    }

    private void OnConnectivityChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ConnectivityService.IsOnline) && connectivity.IsOnline && IsEnabled)
            _ = SyncAsync();
    }

    public async Task<bool> SyncAsync()
    {
        if (!IsEnabled) return true;
        if (!await _lock.WaitAsync(0)) return true;
        try
        {
            var pushedAll = await PushOutboxAsync();
            if (pushedAll)
                await PullSnapshotAsync();
            return pushedAll;
        }
        catch
        {
            return false;
        }
        finally
        {
            _lock.Release();
            StateChanged?.Invoke();
        }
    }

    private async Task PullSnapshotAsync()
    {
        var warehouseId = SettingsService.Instance.OfflineWarehouseId;
        if (warehouseId == 0) return;
        var snapshot = await offlineApi.GetSnapshotAsync(warehouseId, SettingsService.Instance.DeviceId);
        await store.ReplaceSnapshotAsync(
            snapshot.Products.Select(p => new OfflineProduct
            {
                VariantId = p.VariantId,
                ProductName = p.ProductName,
                CategoryName = p.CategoryName,
                UnitName = p.UnitName,
                Quantity = p.Quantity,
                SellingPrice = p.SellingPrice
            }),
            snapshot.Barcodes.Select(b => new OfflineBarcode { Code = b.Code, VariantId = b.VariantId, PackQty = b.PackQty }),
            snapshot.Customers.Select(c => new OfflineCustomer
            {
                Id = c.Id,
                FullName = c.FullName,
                Phone = c.Phone,
                CardBarcode = c.CardBarcode,
                DiscountPct = c.DiscountPct,
                DebtBalance = c.DebtBalance,
                CreditLimit = c.CreditLimit
            }));
        await store.SetMetaAsync("base_currency", snapshot.BaseCurrency);
        await store.SetMetaAsync("last_sync", DateTime.Now.ToString("dd.MM.yyyy HH:mm"));
    }

    private async Task<bool> PushOutboxAsync()
    {
        foreach (var item in await store.GetOutboxAsync("pending"))
        {
            try
            {
                if (item.Kind == "sale")
                {
                    var d = JsonSerializer.Deserialize<OfflineSaleDraft>(item.PayloadJson)!;
                    var items = d.Items.Select(i => new CreateSaleItemRequest(i.VariantId, i.Quantity, i.UnitPrice)).ToList();
                    await salesApi.CreateAsync(new CreateSaleRequest(d.WarehouseId, d.CustomerId, d.PaidCash, d.PaidCard, 0, items,
                        d.DiscountAmount, IdempotencyKey: item.Key, ApplyAutoDiscount: false));
                }
                else
                {
                    var r = JsonSerializer.Deserialize<OfflineRepayDraft>(item.PayloadJson)!;
                    await customersApi.RepayDebtAsync(r.CustomerId, new RepayDebtRequest(r.Amount, false, IdempotencyKey: item.Key));
                }
                item.Status = "done";
                item.Error = null;
                await store.UpdateOutboxAsync(item);
            }
            catch (ApiException ex) when ((int)ex.StatusCode is >= 400 and < 500)
            {
                item.Status = "error";
                item.Error = ApiErrors.Describe(ex);
                await store.UpdateOutboxAsync(item);
            }
            catch
            {
                return false;
            }
        }
        return true;
    }

    public async Task EnqueueSaleAsync(OfflineSaleDraft draft)
    {
        await store.EnqueueAsync(new OfflineOutboxItem
        {
            Kind = "sale",
            Key = Guid.NewGuid().ToString("N"),
            PayloadJson = JsonSerializer.Serialize(draft),
            CreatedAt = DateTime.Now
        });
        foreach (var line in draft.Items)
            await store.AdjustStockAsync(line.VariantId, -line.Quantity);
        _ = Task.Run(SyncAsync);
    }

    public Task<int> PendingCountAsync() => store.CountOutboxAsync("pending");
    public Task<int> ErrorCountAsync() => store.CountOutboxAsync("error");
    public Task<string?> LastSyncTextAsync() => store.GetMetaAsync("last_sync");
}
