using System.Text.Json;
using Cartex.ApiClient.Api;
using Cartex.Mobile.Agent.Data;
using Cartex.Mobile.Agent.Models;
using Cartex.Shared.Models.Customers;
using Cartex.Shared.Models.Ordering;
using Cartex.Shared.Models.Sales;
using Microsoft.Maui.Networking;
using Refit;

namespace Cartex.Mobile.Agent.Services;

public sealed class SyncService(IAgentApi agentApi, ISalesApi salesApi, ICustomersApi customersApi, IOrderingApi orderingApi, AgentDb db)
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    public event Action? StateChanged;
    public DateTime? LastAttempt { get; private set; }
    public bool IsOffline { get; private set; }
    public string? LastError { get; private set; }

    private bool _watching;

    public void StartConnectivityWatch()
    {
        if (_watching) return;
        _watching = true;
        Connectivity.Current.ConnectivityChanged += async (_, e) =>
        {
            if (e.NetworkAccess == NetworkAccess.Internet && await db.CountOutboxAsync("pending") > 0)
                await SyncAsync();
        };
    }

    public async Task<bool> SyncAsync()
    {
        if (!await _lock.WaitAsync(0)) return !IsOffline;
        try
        {
            LastAttempt = DateTime.Now;
            LastError = null;
            var pushedAll = await PushOutboxAsync();
            if (pushedAll)
                await PullAsync();
            IsOffline = !pushedAll;
            return pushedAll;
        }
        catch (ApiException ex)
        {
            IsOffline = false;
            LastError = DescribeError(ex);
            return false;
        }
        catch
        {
            IsOffline = true;
            return false;
        }
        finally
        {
            _lock.Release();
            StateChanged?.Invoke();
        }
    }

    private async Task<bool> PushOutboxAsync()
    {
        var pending = await db.GetPendingOutboxAsync();
        foreach (var item in pending)
        {
            try
            {
                await SendAsync(item);
                item.Status = "done";
                item.Error = null;
                await db.UpdateOutboxAsync(item);
            }
            catch (ApiException ex) when ((int)ex.StatusCode is >= 400 and < 500)
            {
                item.Status = "error";
                item.Error = DescribeError(ex);
                await db.UpdateOutboxAsync(item);
            }
            catch
            {
                return false;
            }
        }
        return true;
    }

    private async Task SendAsync(OutboxItem item)
    {
        switch (item.Kind)
        {
            case "sale":
            {
                var d = JsonSerializer.Deserialize<SaleDraft>(item.PayloadJson)!;
                var items = d.Items.Select(i => new CreateSaleItemRequest(i.VariantId, i.Quantity, i.UnitPrice)).ToList();
                var result = await salesApi.CreateAsync(new CreateSaleRequest(d.WarehouseId, d.CustomerId, d.PaidCash, 0, 0, items,
                    DebtDueDate: d.DebtDueDate, IdempotencyKey: item.Key, ApplyAutoDiscount: false));
                item.ReceiptToken = result.ReceiptToken;
                break;
            }
            case "repay":
            {
                var r = JsonSerializer.Deserialize<RepayDraft>(item.PayloadJson)!;
                await customersApi.RepayDebtAsync(r.CustomerId, new RepayDebtRequest(r.Amount, false, IdempotencyKey: item.Key));
                break;
            }
            case "cart":
            {
                var o = JsonSerializer.Deserialize<OrderDraft>(item.PayloadJson)!;
                var req = new SubmitCartRequest(o.WarehouseId, o.CustomerId,
                    o.Items.Select(i => new SubmitCartItemRequest(i.VariantId, i.Quantity)).ToList(), IdempotencyKey: item.Key);
                var code = await orderingApi.SubmitAsync(req);
                await db.SetOrderCodeAsync(o.LocalId, code);
                break;
            }
            case "checkout":
            {
                var c = JsonSerializer.Deserialize<CheckoutDraft>(item.PayloadJson)!;
                await orderingApi.CheckoutAsync(c.Code, new CheckoutCartRequest(c.PaidCash, 0, 0, IdempotencyKey: item.Key));
                await db.SetOrderStatusByCodeAsync(c.Code, "delivered");
                break;
            }
        }
    }

    public async Task DeleteAsync(OutboxItem item)
    {
        if (item.Status != "done")
        {
            switch (item.Kind)
            {
                case "sale":
                {
                    var d = JsonSerializer.Deserialize<SaleDraft>(item.PayloadJson)!;
                    foreach (var line in d.Items)
                        await db.AdjustStockAsync(line.VariantId, line.Quantity);
                    var total = d.Items.Sum(i => i.Quantity * i.UnitPrice);
                    if (d.CustomerId is { } customerId && total > d.PaidCash)
                        await db.AdjustDebtAsync(customerId, -(total - d.PaidCash));
                    break;
                }
                case "repay":
                {
                    var r = JsonSerializer.Deserialize<RepayDraft>(item.PayloadJson)!;
                    await db.AdjustDebtAsync(r.CustomerId, r.Amount);
                    break;
                }
                case "cart":
                {
                    var o = JsonSerializer.Deserialize<OrderDraft>(item.PayloadJson)!;
                    await db.DeleteOrderAsync(o.LocalId);
                    break;
                }
                case "checkout":
                {
                    var c = JsonSerializer.Deserialize<CheckoutDraft>(item.PayloadJson)!;
                    foreach (var line in c.Items)
                        await db.AdjustStockAsync(line.VariantId, line.Quantity);
                    var debt = c.Total - c.PaidCash;
                    if (c.CustomerId is { } customerId && debt > 0)
                        await db.AdjustDebtAsync(customerId, -debt);
                    await db.SetOrderStatusByCodeAsync(c.Code, "synced");
                    break;
                }
            }
        }
        await db.DeleteOutboxAsync(item.Id);
        StateChanged?.Invoke();
    }

    private async Task PullAsync()
    {
        var data = await agentApi.BootstrapAsync();
        await db.ReplaceCustomersAsync(data.Customers.Select(c => new LocalCustomer
        {
            Id = c.Id,
            FullName = c.FullName,
            Phone = c.Phone,
            Address = c.Address,
            Latitude = c.Latitude,
            Longitude = c.Longitude,
            DebtBalance = c.DebtBalance,
            CreditLimit = c.CreditLimit,
            DebtBalancesJson = JsonSerializer.Serialize(c.DebtBalances)
        }));
        await db.ReplaceVanStockAsync(data.VanStock.Select(s => new LocalVanStock
        {
            VariantId = s.VariantId,
            ProductName = s.ProductName,
            CategoryName = s.CategoryName,
            UnitName = s.UnitName,
            Quantity = s.Quantity,
            SellingPrice = s.SellingPrice
        }));
        await db.SetMetaAsync("warehouse_id", data.WarehouseId?.ToString() ?? "");
        await db.SetMetaAsync("warehouse_name", data.WarehouseName ?? "");
        await db.SetMetaAsync("base_currency", data.BaseCurrency);
        await db.SetMetaAsync("last_sync", DateTime.Now.ToString("dd.MM.yyyy HH:mm"));
    }

    public async Task EnqueueSaleAsync(SaleDraft draft)
    {
        await db.EnqueueAsync(new OutboxItem
        {
            Kind = "sale",
            Key = Guid.NewGuid().ToString("N"),
            PayloadJson = JsonSerializer.Serialize(draft),
            CreatedAt = DateTime.Now
        });
        foreach (var line in draft.Items)
            await db.AdjustStockAsync(line.VariantId, -line.Quantity);
        var total = draft.Items.Sum(i => i.Quantity * i.UnitPrice);
        if (draft.CustomerId is { } customerId && total > draft.PaidCash)
            await db.AdjustDebtAsync(customerId, total - draft.PaidCash);
        _ = Task.Run(SyncAsync);
    }

    public async Task EnqueueRepayAsync(RepayDraft draft)
    {
        await db.EnqueueAsync(new OutboxItem
        {
            Kind = "repay",
            Key = Guid.NewGuid().ToString("N"),
            PayloadJson = JsonSerializer.Serialize(draft),
            CreatedAt = DateTime.Now
        });
        await db.AdjustDebtAsync(draft.CustomerId, -draft.Amount);
        _ = Task.Run(SyncAsync);
    }

    public async Task EnqueueOrderAsync(OrderDraft draft)
    {
        await db.SaveOrderAsync(new LocalOrder
        {
            LocalId = draft.LocalId,
            CustomerId = draft.CustomerId,
            CustomerName = draft.CustomerName ?? "",
            ItemsJson = JsonSerializer.Serialize(draft.Items),
            Total = draft.Items.Sum(i => i.Quantity * i.UnitPrice),
            Status = "new",
            CreatedAt = DateTime.Now
        });
        await db.EnqueueAsync(new OutboxItem
        {
            Kind = "cart",
            Key = draft.LocalId,
            PayloadJson = JsonSerializer.Serialize(draft),
            CreatedAt = DateTime.Now
        });
        _ = Task.Run(SyncAsync);
    }

    public async Task EnqueueCheckoutAsync(CheckoutDraft draft)
    {
        await db.EnqueueAsync(new OutboxItem
        {
            Kind = "checkout",
            Key = "co-" + draft.Code,
            PayloadJson = JsonSerializer.Serialize(draft),
            CreatedAt = DateTime.Now
        });
        foreach (var line in draft.Items)
            await db.AdjustStockAsync(line.VariantId, -line.Quantity);
        var debt = draft.Total - draft.PaidCash;
        if (draft.CustomerId is { } customerId && debt > 0)
            await db.AdjustDebtAsync(customerId, debt);
        await db.SetOrderStatusByCodeAsync(draft.Code, "delivered");
        _ = Task.Run(SyncAsync);
    }

    public async Task RetryAsync(OutboxItem item)
    {
        item.Status = "pending";
        item.Error = null;
        await db.UpdateOutboxAsync(item);
        await SyncAsync();
    }

    public static string DescribeError(ApiException ex)
    {
        try
        {
            using var doc = JsonDocument.Parse(ex.Content ?? "");
            var root = doc.RootElement;
            if (root.TryGetProperty("detail", out var detail) && detail.GetString() is { Length: > 0 } d) return d;
            if (root.TryGetProperty("title", out var title) && title.GetString() is { Length: > 0 } t) return t;
        }
        catch { }
        return (int)ex.StatusCode switch
        {
            401 => Loc.Instance["err_session_expired"],
            403 => Loc.Instance["err_forbidden"],
            _ => string.Format(Loc.Instance["err_server_fmt"], (int)ex.StatusCode)
        };
    }
}
