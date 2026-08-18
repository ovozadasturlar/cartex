using System.Text.Json;
using Cartex.ApiClient.Api;
using Cartex.Mobile.Core;
using Cartex.Shared.Models.Customers;
using Cartex.Shared.Models.OfflineCache;
using Cartex.Shared.Models.Partners;
using Cartex.Shared.Models.Products;
using Cartex.Shared.Models.Sales;

namespace Cartex.Mobile.Store.Services;

public sealed record MobileOfflineCredential(
    string DeviceId,
    long LeaseId,
    long WarehouseId,
    long Epoch,
    string Token,
    long LastAcceptedSequence);

public sealed class MobileOfflineService(
    IOfflineCacheApi offlineApi,
    MobileOfflineStore store,
    MobileAuthService auth)
{
    private const string CredentialKey = "offline_lease_v2";
    private const string EnabledKey = "offline_enabled_v2";
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _loopCts;
    private bool _started;
    private MobileOfflineCredential? _credential;
    private DateTime _lastSnapshotAttempt;

    public event Action? StateChanged;
    public bool ServerReachable { get; private set; } = true;
    public MobileOfflineCredential? Credential => _credential;
    public bool IsEnabled => Preferences.Get(EnabledKey, false)
                             && _credential is not null
                             && _credential.DeviceId == auth.DeviceId;
    public bool ShouldUseOffline => IsEnabled
                                    && (Connectivity.Current.NetworkAccess != NetworkAccess.Internet
                                        || !ServerReachable);

    public async Task StartAsync()
    {
        if (_started) return;
        _started = true;
        await store.InitializeAsync();
        _credential = await ReadCredentialAsync();
        if (_credential is not null)
            await store.PrepareLeaseAsync(_credential);
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
        if (!IsEnabled) return;
        StartLoop();
        if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            _ = Task.Run(() => SyncAsync(), _lifetime.Token);
    }

    private void StartLoop()
    {
        if (_loopCts is not null) return;
        var cts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _loopCts = cts;
        _ = RunLoopAsync(cts.Token);
    }

    private void StopLoop()
    {
        Debounce.Cancel(ref _loopCts);
    }

    public void MarkServerUnavailable()
    {
        if (!IsEnabled) return;
        ServerReachable = false;
        StateChanged?.Invoke();
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        if (e.NetworkAccess != NetworkAccess.Internet || !IsEnabled) return;
        _ = Task.Run(() => SyncAsync(), _lifetime.Token);
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(20));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (!IsEnabled || Connectivity.Current.NetworkAccess != NetworkAccess.Internet) continue;
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
        var credential = new MobileOfflineCredential(
            auth.DeviceId, grant.LeaseId, grant.WarehouseId, grant.Epoch,
            grant.LeaseToken, grant.LastAcceptedSequence);
        await SaveCredentialAsync(credential);
        _credential = credential;
        Preferences.Set(EnabledKey, false);
        await store.PrepareLeaseAsync(credential);
        await PullSnapshotAsync(credential);
        Preferences.Set(EnabledKey, true);
        ServerReachable = true;
        StartLoop();
        StateChanged?.Invoke();
    }

    public async Task ReleaseAsync(string? reason = null)
    {
        var credential = _credential
            ?? throw new InvalidOperationException("Oflayn vakolat kaliti topilmadi.");
        await offlineApi.ReleaseAsync(new ReleaseOfflineCacheRequest(
            credential.LeaseId, credential.Token, false, reason));
        await DeactivateLocalAsync();
    }

    public async Task DeactivateLocalAsync()
    {
        StopLoop();
        Preferences.Set(EnabledKey, false);
        SecureStorage.Remove(CredentialKey);
        _credential = null;
        await store.ClearProjectionAsync();
        StateChanged?.Invoke();
    }

    public async Task<bool> SyncAsync()
    {
        if (!IsEnabled || Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return false;
        if (!await _syncLock.WaitAsync(0)) return true;
        try
        {
            var credential = _credential;
            if (credential is null) return false;
            await HeartbeatCoreAsync(credential);
            var pushed = await PushAsync(credential);
            if (pushed)
                await PullSnapshotAsync(_credential ?? credential);
            ServerReachable = true;
            return pushed;
        }
        catch
        {
            ServerReachable = false;
            return false;
        }
        finally
        {
            _syncLock.Release();
            StateChanged?.Invoke();
        }
    }

    private async Task HeartbeatAsync()
    {
        if (!await _syncLock.WaitAsync(0)) return;
        try
        {
            if (_credential is { } credential)
            {
                await HeartbeatCoreAsync(credential);
                ServerReachable = true;
            }
        }
        catch
        {
            ServerReachable = false;
        }
        finally
        {
            _syncLock.Release();
            StateChanged?.Invoke();
        }
    }

    private async Task HeartbeatCoreAsync(MobileOfflineCredential credential)
    {
        var pending = await store.CountAsync(credential.LeaseId, credential.Epoch, "pending")
                      + await store.CountAsync(credential.LeaseId, credential.Epoch, "error");
        var result = await offlineApi.HeartbeatAsync(new OfflineHeartbeatRequest(
            credential.LeaseId, credential.Epoch, credential.Token, pending));
        if (result.LastAcceptedSequence != credential.LastAcceptedSequence)
        {
            credential = credential with { LastAcceptedSequence = result.LastAcceptedSequence };
            await SaveCredentialAsync(credential);
            _credential = credential;
        }
    }

    private async Task PullSnapshotAsync(MobileOfflineCredential credential)
    {
        _lastSnapshotAttempt = DateTime.UtcNow;
        var snapshot = await offlineApi.GetSnapshotAsync(
            credential.LeaseId, credential.Epoch, credential.Token);
        if (snapshot.LeaseId != credential.LeaseId || snapshot.Epoch != credential.Epoch)
            throw new InvalidOperationException("Server boshqa oflayn vakolat snapshotini qaytardi.");
        await store.ReplaceSnapshotAsync(snapshot);
    }

    private async Task<bool> PushAsync(MobileOfflineCredential credential)
    {
        while (true)
        {
            var rows = await store.GetPendingAsync(credential.LeaseId, credential.Epoch, 50);
            if (rows.Count == 0) return true;
            var requests = new List<OfflineSyncEventRequest>(rows.Count);
            foreach (var row in rows)
            {
                using var payload = JsonDocument.Parse(store.ReadPayload(row));
                requests.Add(new OfflineSyncEventRequest(
                    Guid.Parse(row.EventId), row.Sequence, row.Kind, row.IdempotencyKey,
                    row.OccurredAt, payload.RootElement.Clone(), row.ActorUserId));
            }

            var result = await offlineApi.SyncBatchAsync(new OfflineSyncBatchRequest(
                credential.LeaseId, credential.Epoch, credential.Token, requests));
            foreach (var eventResult in result.Results)
            {
                var row = rows.FirstOrDefault(x => x.EventId == eventResult.EventId.ToString("D"));
                if (row is null) continue;
                if (eventResult.Status is "Applied" or "AlreadyApplied")
                {
                    row.Status = "done";
                    row.Error = null;
                }
                else if (eventResult.Status == "Rejected")
                {
                    row.Status = "error";
                    row.Error = $"{eventResult.ErrorCode}: {eventResult.Error}";
                }
                else continue;
                await store.UpdateAsync(row);
            }

            credential = credential with { LastAcceptedSequence = result.LastAcceptedSequence };
            await SaveCredentialAsync(credential);
            _credential = credential;
            if (result.Results.Any(x => x.Status == "Rejected")) return false;
        }
    }

    public async Task EnqueueSaleAsync(CreateSaleRequest request)
    {
        var credential = _credential
            ?? throw new InvalidOperationException(Loc.Instance["offline_not_authority"]);
        if (!IsEnabled || request.WarehouseId != credential.WarehouseId)
            throw new InvalidOperationException(Loc.Instance["offline_wrong_warehouse"]);
        var actorId = auth.UserId
            ?? throw new InvalidOperationException(Loc.Instance["err_session_expired"]);
        var total = Math.Max(0, request.Items.Sum(x => x.Quantity * (x.UnitPrice ?? 0)) - request.DiscountAmount);
        var paid = request.PaidCash + request.PaidCard + request.PaidBonus;
        var debt = Math.Max(0, total - paid);
        if (debt > 0)
        {
            if (request.CustomerId is null)
                throw new InvalidOperationException(Loc.Instance["err_debt_needs_customer"]);
            if (await store.GetMetaAsync("allow_debt_sales") == "0")
                throw new InvalidOperationException(Loc.Instance["debt_sales_disabled"]);
            var customer = await store.GetCustomerAsync(request.CustomerId.Value)
                ?? throw new InvalidOperationException(Loc.Instance["customer"] + " — " + Loc.Instance["offline_not_authority"]);
            if (customer.CreditLimit > 0 && customer.DebtBalance + debt > customer.CreditLimit)
                throw new InvalidOperationException(Loc.Instance["credit_limit_exceeded"]);
        }
        await store.EnqueueSaleAsync(request, credential, actorId);
        StateChanged?.Invoke();
        if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            _ = Task.Run(() => SyncAsync(), _lifetime.Token);
    }

    public async Task<ProductLookupDto?> FindProductAsync(string barcode)
    {
        var found = await store.GetByBarcodeAsync(barcode);
        if (found is null) return null;
        var (product, packQty) = found.Value;
        var baseCurrency = await store.GetMetaAsync("base_currency") ?? "UZS";
        return new ProductLookupDto(product.VariantId, product.ProductName, product.UnitName,
            packQty > 0 ? packQty : 1, product.SellingPrice, product.Quantity,
            product.Dimension, AllowsAmountEntry: product.AllowsAmountEntry,
            OriginalSellingPrice: product.SellingPrice,
            PriceCurrency: baseCurrency, BaseCurrency: baseCurrency,
            AllowsFractional: product.AllowsFractional);
    }

    public async Task<ProductLookupDto?> FindProductByVariantAsync(long variantId, decimal packQty = 1)
    {
        var product = await store.GetProductAsync(variantId);
        if (product is null) return null;
        var baseCurrency = await store.GetMetaAsync("base_currency") ?? "UZS";
        return new ProductLookupDto(product.VariantId, product.ProductName, product.UnitName,
            packQty > 0 ? packQty : 1, product.SellingPrice, product.Quantity,
            product.Dimension, AllowsAmountEntry: product.AllowsAmountEntry,
            OriginalSellingPrice: product.SellingPrice,
            PriceCurrency: baseCurrency, BaseCurrency: baseCurrency,
            AllowsFractional: product.AllowsFractional);
    }

    public async Task<IReadOnlyList<ProductDto>> SearchProductsAsync(string term, int limit)
    {
        var baseCurrency = await store.GetMetaAsync("base_currency") ?? "UZS";
        var rows = await store.SearchProductsAsync(term, limit);
        return rows.Select(x => new ProductDto(
            x.VariantId, x.VariantId, x.ProductName, x.CategoryName, x.UnitName,
            0, [], null, null, false, null, null, null, null, null,
            x.SellingPrice, x.Quantity, PriceCurrency: baseCurrency,
            Dimension: x.Dimension, IsEnabled: true,
            AllowsAmountEntry: x.AllowsAmountEntry,
            AllowsFractional: x.AllowsFractional)).ToList();
    }

    public async Task<IReadOnlyList<CustomerDto>> SearchCustomersAsync(string term, int limit)
    {
        var rows = await store.SearchCustomersAsync(term, limit);
        var baseCurrency = await BaseCurrencyAsync();
        return rows.Select(x => new CustomerDto(
            x.Id, x.FullName, null, null, x.Phone, null, x.CardBarcode,
            x.DiscountPct, 0, x.DebtBalance, x.CreditLimit)
        {
            DebtBalances = x.DebtBalance > 0
                ? [new Cartex.Shared.Models.Common.CurrencyAmountDto(baseCurrency, x.DebtBalance)]
                : []
        }).ToList();
    }

    public async Task<IReadOnlyList<ParticipantRoleDto>> GetParticipantRolesAsync()
    {
        var rows = await store.GetRolesAsync();
        return rows.Select(x => new ParticipantRoleDto(
            x.Id, x.Key, x.Label, x.Label, true, x.IsRequired, x.CanEqualBuyer,
            x.MaxCount, true, true, x.SortOrder)).ToList();
    }

    public async Task<IReadOnlyList<PartnerDto>> SearchPartnersAsync(string term, int limit)
    {
        var rows = await store.SearchPartnersAsync(term, limit);
        return rows.Select(ToPartner).ToList();
    }

    public async Task<PartnerDto?> FindPartnerByCustomerAsync(long customerId)
    {
        var row = await store.FindPartnerByCustomerAsync(customerId);
        return row is null ? null : ToPartner(row);
    }

    public async Task<int> PendingCountAsync()
    {
        var c = _credential;
        return c is null ? 0 : await store.CountAsync(c.LeaseId, c.Epoch, "pending");
    }

    public async Task<int> ErrorCountAsync()
    {
        var c = _credential;
        return c is null ? 0 : await store.CountAsync(c.LeaseId, c.Epoch, "error");
    }

    public Task<string?> LastSyncAsync() => store.GetMetaAsync("last_sync");
    public async Task<string> BaseCurrencyAsync() => await store.GetMetaAsync("base_currency") ?? "UZS";

    private static PartnerDto ToPartner(MobileOfflinePartner x) => new(
        x.PartnerId, x.PartyId, x.PartnerCode, x.FullName, x.Phone,
        null, null, x.CustomerId, true, DateOnly.MinValue, 0, 0, 0, 0, null);

    private async Task<MobileOfflineCredential?> ReadCredentialAsync()
    {
        try
        {
            var json = await SecureStorage.GetAsync(CredentialKey);
            return string.IsNullOrWhiteSpace(json)
                ? null
                : JsonSerializer.Deserialize<MobileOfflineCredential>(json);
        }
        catch
        {
            return null;
        }
    }

    private static Task SaveCredentialAsync(MobileOfflineCredential credential) =>
        SecureStorage.SetAsync(CredentialKey, JsonSerializer.Serialize(credential));
}
