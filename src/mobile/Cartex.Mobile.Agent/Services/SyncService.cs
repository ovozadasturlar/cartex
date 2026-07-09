using System.Text.Json;
using Cartex.ApiClient.Api;
using Cartex.Mobile.Agent.Data;
using Cartex.Mobile.Agent.Models;
using Cartex.Shared.Models.Customers;
using Cartex.Shared.Models.Sales;
using Refit;

namespace Cartex.Mobile.Agent.Services;

public sealed class SyncService(IAgentApi agentApi, ISalesApi salesApi, ICustomersApi customersApi, AgentDb db)
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    public event Action? StateChanged;
    public DateTime? LastAttempt { get; private set; }
    public bool IsOffline { get; private set; }
    public string? LastError { get; private set; }

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
        if (item.Kind == "sale")
        {
            var d = JsonSerializer.Deserialize<SaleDraft>(item.PayloadJson)!;
            var items = d.Items.Select(i => new CreateSaleItemRequest(i.VariantId, i.Quantity, i.UnitPrice)).ToList();
            await salesApi.CreateAsync(new CreateSaleRequest(d.WarehouseId, d.CustomerId, d.PaidCash, 0, 0, items,
                DebtDueDate: d.DebtDueDate, IdempotencyKey: item.Key, ApplyAutoDiscount: false));
        }
        else
        {
            var r = JsonSerializer.Deserialize<RepayDraft>(item.PayloadJson)!;
            await customersApi.RepayDebtAsync(r.CustomerId, new RepayDebtRequest(r.Amount, false, IdempotencyKey: item.Key));
        }
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
            401 => "Sessiya tugagan — qayta kiring",
            403 => "Ruxsat yo'q yoki modul yoqilmagan",
            _ => $"Server xatosi ({(int)ex.StatusCode})"
        };
    }
}
