using Cartex.Shared.Models.Settings;
using System.Net;
using Refit;

namespace Cartex.Mobile.Core;

public enum AccessLoadState
{
    Loading,
    Loaded,
    Failed
}

public enum AccessFailureKind
{
    Network,
    Server,
    SessionInvalid,
    Unknown
}

public sealed record AccessSnapshot(
    IReadOnlySet<string> Permissions,
    IReadOnlySet<string> Features,
    SalesPolicyDto SalesPolicy,
    DateTimeOffset? LastSuccessfulRefresh);

public interface IAccessStateLoader
{
    bool IsOnline { get; }
    event Action? ConnectivityChanged;
    Task<AccessSnapshot> LoadAsync(bool refresh, CancellationToken cancellationToken);
    void Clear();
}

public sealed class AccessState
{
    private static readonly AccessSnapshot Empty = new(
        new HashSet<string>(StringComparer.Ordinal),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        new SalesPolicyDto(),
        null);

    private readonly IAccessStateLoader _loader;
    private readonly TimeProvider _timeProvider;
    private readonly Lock _sync = new();
    private AccessSnapshot _snapshot = Empty;
    private Task? _loading;
    private Task? _refreshing;
    private PeriodicTimer? _retryTimer;
    private int _retryAttempt;
    private int _generation;

    public AccessState(IAccessStateLoader loader, TimeProvider? timeProvider = null)
    {
        _loader = loader;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _loader.ConnectivityChanged += OnConnectivityChanged;
    }

    public AccessLoadState State { get; private set; } = AccessLoadState.Loading;
    public AccessFailureKind? FailureKind { get; private set; }
    public bool IsLoaded => State == AccessLoadState.Loaded;
    public DateTimeOffset? LastSuccessfulRefresh => _snapshot.LastSuccessfulRefresh;
    public SalesPolicyDto SalesPolicy => _snapshot.SalesPolicy;
    public bool CartsEnabled => Feature("ordering") || Feature("store");
    public bool OfflineCacheEnabled => Feature("offline_cache");
    public bool CanSell => HasAny("sales.create", "sales.checkout") && CartsEnabled;
    public bool CanCreateSale => Has("sales.create") && CartsEnabled;
    public bool CanCheckout => Has("sales.checkout") && CartsEnabled;
    public bool CanQueue => HasAny("sales.pick", "sales.view")
        && SalesPolicy.AllowSaleQueue
        && CartsEnabled;
    public bool CanUseCart => CanSell || CanQueue;
    public bool CanReceiveStock => Has("supplies.create") && Feature("supplies");
    public bool CanSearchProducts => Has("products.view");
    public bool CanEditProduct => Has("products.edit");
    public bool CanCreateProduct => Has("products.create");
    public bool CanCreateBarcode => Has("barcodes.create");
    public bool CanDeleteBarcode => Has("barcodes.delete");
    public bool CanUseRemotePrinting => Has("printing.remote.use") && Feature("remote_printing");
    public bool CanPrintBarcode => CanUseRemotePrinting && Has("printing.barcodes.print");
    public bool CanPrintReceipt => CanUseRemotePrinting && Has("printing.receipts.print");
    public bool CanReprintReceipt => CanUseRemotePrinting && Has("printing.receipts.reprint");
    public bool CanPrintZReport => CanUseRemotePrinting && Has("printing.z_reports.print");
    public bool CanOverridePrice => Has("sales.priceOverride");
    public bool CanDiscount => Has("sales.discount");
    public bool CanVoidSale => Has("sales.void");
    public bool CanViewSales => HasAny("sales.view", "sales.viewAll");
    public bool CanViewShifts => HasAny("shifts.view", "shifts.viewAll");
    public bool CanViewTrade => CanQueue || CanViewSales || CanViewShifts;
    public bool CanViewHome => CanSell || CanQueue || CanViewSales || CanReceiveStock;
    public bool CanUseScanner => CanUseCart || CanReceiveStock || CanSearchProducts
        || CanEditProduct || CanPrintBarcode;
    public bool CanViewCustomers => Has("customers.view");
    public bool CanCreateCustomer => Has("customers.create");
    public bool CanReceiveCustomerPayment => Has("customer_payments.create");
    public bool CanViewCustomerPayments => Has("customer_payments.view");
    public bool CanWriteOffDebt => Has("customer_payments.writeOffDebt")
        && SalesPolicy.AllowDebtWriteOff;
    public bool CanMessageCustomer => Has("customers.message");
    public bool CanViewCustomerStatement => Has("statements.view");
    public bool CanExportCustomerStatement => Has("statements.export");
    public bool CanRefundCustomer => Has("customers.refund");
    public bool CanViewReturns => Has("returns.view");
    public bool CanViewDevices => Has("devices.view");
    public bool CanManageOffline => Has("devices.revoke") && CanSell && OfflineCacheEnabled;
    public bool CanHostSms => Has("sms.gateway.host");
    public bool CanViewPartners => Has("partners.view");
    public bool CanEditPartners => Has("partners.edit");
    public bool CanUsePricingMulticurrency => Feature("multicurrency_pricing");
    public bool CanUseAgent => Feature("agents");
    public bool CanAgentViewCatalog => CanUseAgent && Has("products.view");
    public bool CanAgentEditProduct => CanUseAgent && Has("products.edit");
    public bool CanAgentUseCart => CanUseAgent && Has("sales.create");
    public bool CanAgentViewCustomers => CanUseAgent && Has("customers.view");
    public bool CanAgentViewOrders => CanUseAgent && Has("sales.view");
    public bool IsStale => IsLoaded
        && !_loader.IsOnline
        && (LastSuccessfulRefresh is null
            || _timeProvider.GetUtcNow() - LastSuccessfulRefresh.Value > TimeSpan.FromMinutes(15));

    public event Action? Changed;

    public bool Has(string permission) => IsLoaded
        && (_snapshot.Permissions.Contains("*") || _snapshot.Permissions.Contains(permission));

    public bool HasAny(params string[] permissions) => permissions.Any(Has);

    public bool Feature(string code) => IsLoaded && _snapshot.Features.Contains(code);

    public Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (IsLoaded)
            {
                var isExpired = LastSuccessfulRefresh is null
                    || _timeProvider.GetUtcNow() - LastSuccessfulRefresh.Value > TimeSpan.FromMinutes(15);
                if (!_loader.IsOnline || !isExpired) return Task.CompletedTask;
                return _loading is { IsCompleted: false } refresh
                    ? refresh
                    : _loading = LoadAsync(refresh: true, cancellationToken, _generation);
            }
            return _loading is { IsCompleted: false } loading
                ? loading
                : _loading = LoadAsync(refresh: false, cancellationToken, _generation);
        }
    }

    public Task RefreshAsync()
    {
        lock (_sync)
        {
            CancelRetry();
            return _refreshing is { IsCompleted: false } refreshing
                ? refreshing
                : _refreshing = RefreshCoreAsync(_generation);
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _generation++;
            _snapshot = Empty;
            _loading = null;
            _refreshing = null;
            CancelRetry();
            _retryAttempt = 0;
            State = AccessLoadState.Loading;
            FailureKind = null;
        }
        _loader.Clear();
        Changed?.Invoke();
    }

    private async Task RefreshCoreAsync(int generation)
    {
        try
        {
            Task pending;
            lock (_sync)
            {
                if (generation != _generation) return;
                pending = _loading ?? Task.CompletedTask;
            }
            await pending;

            Task refresh;
            lock (_sync)
            {
                if (generation != _generation) return;
                refresh = _loading is { IsCompleted: false } loading
                    ? loading
                    : _loading = LoadAsync(refresh: true, CancellationToken.None, generation);
            }
            await refresh;
        }
        finally
        {
            lock (_sync)
                if (generation == _generation) _refreshing = null;
        }
    }

    private async Task LoadAsync(bool refresh, CancellationToken cancellationToken, int generation)
    {
        var notifyLoading = false;
        try
        {
            lock (_sync)
            {
                if (generation != _generation) return;
                if (!IsLoaded)
                {
                    notifyLoading = State != AccessLoadState.Loading || FailureKind is not null;
                    State = AccessLoadState.Loading;
                    FailureKind = null;
                }
            }
            if (notifyLoading) Changed?.Invoke();

            var snapshot = await _loader.LoadAsync(refresh, cancellationToken);
            bool changed;
            lock (_sync)
            {
                if (generation != _generation) return;
                changed = State != AccessLoadState.Loaded || !Equivalent(_snapshot, snapshot);
                _snapshot = snapshot;
                State = AccessLoadState.Loaded;
                FailureKind = null;
                _retryAttempt = 0;
                CancelRetry();
            }
            if (changed) Changed?.Invoke();
        }
        catch (Exception exception)
        {
            AccessFailureKind failure;
            lock (_sync)
            {
                if (generation != _generation) return;
                failure = Classify(exception);
                _snapshot = Empty;
                State = AccessLoadState.Failed;
                FailureKind = failure;
            }
            Changed?.Invoke();
            if (failure != AccessFailureKind.SessionInvalid)
                ScheduleRetry(generation);
        }
        finally
        {
            lock (_sync)
                if (generation == _generation) _loading = null;
        }
    }

    private void OnConnectivityChanged()
    {
        Changed?.Invoke();
        if (_loader.IsOnline && State == AccessLoadState.Failed)
            _ = RefreshAsync();
    }

    private void ScheduleRetry(int generation)
    {
        TimeSpan delay;
        PeriodicTimer timer;
        lock (_sync)
        {
            if (generation != _generation || State != AccessLoadState.Failed || !_loader.IsOnline)
                return;
            CancelRetry();
            delay = TimeSpan.FromSeconds(Math.Min(Math.Pow(2, _retryAttempt++), 30));
            timer = _retryTimer = new PeriodicTimer(delay, _timeProvider);
        }
        _ = RetryAfterAsync(timer, generation);
    }

    private async Task RetryAfterAsync(PeriodicTimer timer, int generation)
    {
        try
        {
            if (!await timer.WaitForNextTickAsync()) return;
            Task loading;
            lock (_sync)
            {
                if (generation != _generation || State != AccessLoadState.Failed) return;
                loading = _loading is { IsCompleted: false } current
                    ? current
                    : _loading = LoadAsync(refresh: true, CancellationToken.None, generation);
            }
            await loading;
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void CancelRetry()
    {
        _retryTimer?.Dispose();
        _retryTimer = null;
    }

    private static AccessFailureKind Classify(Exception exception)
    {
        if (exception is ApiException { StatusCode: HttpStatusCode.Unauthorized })
            return AccessFailureKind.SessionInvalid;
        if (exception is UnauthorizedAccessException)
            return AccessFailureKind.SessionInvalid;
        if (exception is ApiException { StatusCode: >= HttpStatusCode.InternalServerError })
            return AccessFailureKind.Server;
        if (exception is HttpRequestException or TaskCanceledException
            || exception.InnerException is HttpRequestException or System.Net.Sockets.SocketException)
            return AccessFailureKind.Network;
        return AccessFailureKind.Unknown;
    }

    private static bool Equivalent(AccessSnapshot left, AccessSnapshot right) =>
        left.Permissions.SetEquals(right.Permissions)
        && left.Features.SetEquals(right.Features)
        && left.SalesPolicy == right.SalesPolicy
        && left.LastSuccessfulRefresh == right.LastSuccessfulRefresh;
}
