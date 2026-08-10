using System.ComponentModel;
using System.Text.Json;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.OfflineCache;
using Cartex.Shared.Models.Sales;

namespace Cartex.UI.Services;

public sealed record OfflineSaleItemDraft(long VariantId, decimal Quantity, decimal? UnitPrice);

public sealed record OfflineSaleDraft(
    long WarehouseId,
    long? CustomerId,
    decimal PaidCash,
    decimal PaidCard,
    List<OfflineSaleItemDraft> Items,
    decimal DiscountAmount,
    DateOnly? DebtDueDate = null);

public sealed class OfflineSyncService(
    IOfflineCacheApi offlineApi,
    OfflineStore store,
    OfflineLeaseCredentialStore credentials,
    ConnectivityService connectivity,
    AuthService auth)
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private bool _hooked;
    private DateTime _lastSnapshotAttempt;

    public event Action? StateChanged;

    public OfflineLeaseCredential? Credential => credentials.Load();
    public bool IsEnabled
    {
        get
        {
            var credential = Credential;
            return SettingsService.Instance.OfflineCacheEnabled
                   && credential is not null
                   && credential.DeviceId == SettingsService.Instance.DeviceId
                   && credential.WarehouseId == SettingsService.Instance.OfflineWarehouseId;
        }
    }

    public void Start()
    {
        if (_hooked) return;
        _hooked = true;
        connectivity.PropertyChanged += OnConnectivityChanged;
        _ = RunHeartbeatLoopAsync(_lifetime.Token);
        if (IsEnabled && connectivity.IsOnline)
            _ = Task.Run(() => SyncAsync(), _lifetime.Token);
    }

    private void OnConnectivityChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ConnectivityService.IsOnline) && connectivity.IsOnline && IsEnabled)
            _ = Task.Run(() => SyncAsync(), _lifetime.Token);
    }

    private async Task RunHeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(20));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (!IsEnabled || !connectivity.IsOnline) continue;
                var pending = await PendingCountAsync();
                if (pending > 0 || DateTime.UtcNow - _lastSnapshotAttempt > TimeSpan.FromMinutes(5))
                    await SyncAsync();
                else
                    await HeartbeatAsync();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    public async Task ActivateAsync(OfflineLeaseGrantDto grant)
    {
        var credential = new OfflineLeaseCredential(
            SettingsService.Instance.DeviceId,
            grant.LeaseId,
            grant.WarehouseId,
            grant.Epoch,
            grant.LeaseToken,
            grant.LastAcceptedSequence);
        credentials.Save(credential);
        SettingsService.Instance.OfflineWarehouseId = grant.WarehouseId;
        SettingsService.Instance.OfflineCacheEnabled = false;
        await store.PrepareLeaseAsync(credential);
        await PullSnapshotAsync(credential);
        SettingsService.Instance.OfflineCacheEnabled = true;
        StateChanged?.Invoke();
    }

    public async Task ReleaseAsync(string? reason = null)
    {
        var credential = Credential
            ?? throw new InvalidOperationException("Oflayn vakolat kaliti topilmadi.");
        await offlineApi.ReleaseAsync(new ReleaseOfflineCacheRequest(
            credential.LeaseId, credential.Token, false, reason));
        await DeactivateLocalAsync();
    }

    public async Task DeactivateLocalAsync()
    {
        SettingsService.Instance.OfflineCacheEnabled = false;
        credentials.Clear();
        await store.ClearAsync();
        StateChanged?.Invoke();
    }

    public async Task<bool> SyncAsync()
    {
        if (!IsEnabled || !connectivity.IsOnline) return false;
        if (!await _lock.WaitAsync(0)) return true;
        try
        {
            var credential = Credential;
            if (credential is null) return false;
            await HeartbeatCoreAsync(credential);
            var pushedAll = await PushOutboxAsync(credential);
            if (pushedAll)
            {
                credential = Credential ?? credential;
                await PullSnapshotAsync(credential);
            }
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

    private async Task HeartbeatAsync()
    {
        if (!await _lock.WaitAsync(0)) return;
        try
        {
            if (Credential is { } credential)
                await HeartbeatCoreAsync(credential);
        }
        catch
        {
        }
        finally
        {
            _lock.Release();
            StateChanged?.Invoke();
        }
    }

    private async Task HeartbeatCoreAsync(OfflineLeaseCredential credential)
    {
        var pending = await store.CountOutboxAsync(credential.LeaseId, credential.Epoch, "pending")
                      + await store.CountOutboxAsync(credential.LeaseId, credential.Epoch, "error");
        var response = await offlineApi.HeartbeatAsync(new OfflineHeartbeatRequest(
            credential.LeaseId, credential.Epoch, credential.Token, pending));
        if (response.LastAcceptedSequence != credential.LastAcceptedSequence)
            credentials.Save(credential with { LastAcceptedSequence = response.LastAcceptedSequence });
    }

    private async Task PullSnapshotAsync(OfflineLeaseCredential credential)
    {
        _lastSnapshotAttempt = DateTime.UtcNow;
        var snapshot = await offlineApi.GetSnapshotAsync(
            credential.LeaseId, credential.Epoch, credential.Token);
        if (snapshot.LeaseId != credential.LeaseId || snapshot.Epoch != credential.Epoch)
            throw new InvalidOperationException("Server boshqa oflayn vakolat snapshotini qaytardi.");

        await store.ReplaceSnapshotAsync(
            snapshot.Products.Select(p => new OfflineProduct
            {
                VariantId = p.VariantId,
                ProductName = p.ProductName,
                CategoryName = p.CategoryName,
                UnitName = p.UnitName,
                Quantity = p.Quantity,
                SellingPrice = p.SellingPrice,
                AllowsAmountEntry = p.AllowsAmountEntry,
                AllowsFractional = p.AllowsFractional
            }),
            snapshot.Barcodes.Select(b => new OfflineBarcode
            {
                Code = b.Code,
                VariantId = b.VariantId,
                PackQty = b.PackQty
            }),
            snapshot.Customers.Select(c => new OfflineCustomer
            {
                Id = c.Id,
                FullName = c.FullName,
                Phone = c.Phone,
                CardBarcode = c.CardBarcode,
                DiscountPct = c.DiscountPct,
                DebtBalance = c.DebtBalance,
                CreditLimit = c.CreditLimit
            }),
            credential.LeaseId,
            credential.Epoch,
            snapshot.SnapshotVersion);
        await store.SetMetaAsync("base_currency", snapshot.BaseCurrency);
        await store.SetMetaAsync("allow_debt_sales", snapshot.AllowDebtSales ? "1" : "0");
        await store.SetMetaAsync("allow_insufficient_stock_sales",
            snapshot.AllowInsufficientStockSales ? "1" : "0");
        await store.SetMetaAsync("last_sync", snapshot.ServerTime.ToLocalTime().ToString("dd.MM.yyyy HH:mm"));
    }

    private async Task<bool> PushOutboxAsync(OfflineLeaseCredential credential)
    {
        while (true)
        {
            var rows = await store.GetOutboxAsync(
                credential.LeaseId, credential.Epoch, "pending", 50);
            if (rows.Count == 0) return true;

            var eventRows = new List<OfflineSyncEventRequest>(rows.Count);
            foreach (var item in rows)
            {
                using var document = JsonDocument.Parse(item.PayloadJson);
                eventRows.Add(new OfflineSyncEventRequest(
                    Guid.Parse(item.EventId),
                    item.Sequence,
                    item.Kind,
                    item.Key,
                    item.OccurredAt,
                    document.RootElement.Clone(),
                    item.ActorUserId));
            }

            var result = await offlineApi.SyncBatchAsync(new OfflineSyncBatchRequest(
                credential.LeaseId, credential.Epoch, credential.Token, eventRows));
            foreach (var eventResult in result.Results)
            {
                var item = rows.FirstOrDefault(x => x.EventId == eventResult.EventId.ToString("D"));
                if (item is null) continue;
                switch (eventResult.Status)
                {
                    case "Applied":
                    case "AlreadyApplied":
                        item.Status = "done";
                        item.Error = null;
                        break;
                    case "Rejected":
                        item.Status = "error";
                        item.Error = $"{eventResult.ErrorCode}: {eventResult.Error}";
                        break;
                    default:
                        continue;
                }
                await store.UpdateOutboxAsync(item);
            }

            credential = credential with { LastAcceptedSequence = result.LastAcceptedSequence };
            credentials.Save(credential);
            if (result.Results.Any(x => x.Status == "Rejected")) return false;
        }
    }

    public async Task EnqueueSaleAsync(OfflineSaleDraft draft)
    {
        var credential = Credential
            ?? throw new InvalidOperationException("Bu qurilmada oflayn savdo vakolati yo'q.");
        if (!IsEnabled || draft.WarehouseId != credential.WarehouseId)
            throw new InvalidOperationException("Savdo faqat oflayn vakolatga biriktirilgan omborda bajariladi.");
        var actorId = auth.UserInfo?.UserId ?? 0;
        if (actorId <= 0)
            throw new InvalidOperationException("Oflayn savdo foydalanuvchisi aniqlanmadi.");

        var idempotencyKey = Guid.NewGuid().ToString("N");
        var request = new CreateSaleRequest(
            draft.WarehouseId,
            draft.CustomerId,
            draft.PaidCash,
            draft.PaidCard,
            0,
            draft.Items.Select(x => new CreateSaleItemRequest(
                x.VariantId, x.Quantity, x.UnitPrice)).ToList(),
            draft.DiscountAmount,
            DebtDueDate: draft.DebtDueDate,
            IdempotencyKey: idempotencyKey,
            ApplyAutoDiscount: false,
            UseCustomerAdvance: false);
        var payload = JsonSerializer.Serialize(request,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await store.EnqueueSaleAndAdjustStockAsync(payload, idempotencyKey, credential, actorId,
            await store.GetAllowInsufficientStockSalesAsync());
        StateChanged?.Invoke();
        if (connectivity.IsOnline)
            _ = Task.Run(() => SyncAsync(), _lifetime.Token);
    }

    public async Task<int> PendingCountAsync()
    {
        var credential = Credential;
        return credential is null ? 0 : await store.CountOutboxAsync(
            credential.LeaseId, credential.Epoch, "pending");
    }

    public async Task<int> ErrorCountAsync()
    {
        var credential = Credential;
        return credential is null ? 0 : await store.CountOutboxAsync(
            credential.LeaseId, credential.Epoch, "error");
    }

    public Task<string?> LastSyncTextAsync() => store.GetMetaAsync("last_sync");
}
