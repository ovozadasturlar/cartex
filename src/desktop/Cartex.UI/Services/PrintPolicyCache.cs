using System.ComponentModel;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Printing;
using Cartex.Shared.Models.Settings;

namespace Cartex.UI.Services;

public sealed record LocalPrintPolicy(
    PrintRoutingPolicyDto Routing,
    SalesPolicyDto Sales,
    bool HasEnabledLocalEndpoint);

public static class LocalPrintRouting
{
    public static bool ShouldPrintLocally(
        PrintRoutingPolicyDto? policy,
        bool canPrintLocally,
        bool hasLocalPrinter,
        bool salesPolicyAllows) =>
        canPrintLocally
        && hasLocalPrinter
        && salesPolicyAllows
        && policy is { IsEnabled: true, RoutingMode: PrintRoutingMode.LocalFirst };
}

public sealed class PrintPolicyCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
    private readonly IPrintingApi _printing;
    private readonly ISettingsApi _settings;
    private readonly ReferenceCache _referenceCache;
    private readonly AuthService _auth;
    private readonly BranchContextService _branch;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private Snapshot? _snapshot;

    public PrintPolicyCache(
        IPrintingApi printing,
        ISettingsApi settings,
        ReferenceCache referenceCache,
        AuthService auth,
        BranchContextService branch)
    {
        _printing = printing;
        _settings = settings;
        _referenceCache = referenceCache;
        _auth = auth;
        _branch = branch;
        _auth.LoggedIn += RefreshCurrent;
        _auth.LoggedOut += Invalidate;
        _branch.PropertyChanged += BranchChanged;
    }

    public async Task<LocalPrintPolicy?> GetAsync(
        long branchId,
        PrintJobKind kind,
        CancellationToken cancellationToken)
    {
        var snapshot = _snapshot;
        if (snapshot is not null && snapshot.BranchId == branchId && DateTime.UtcNow - snapshot.LoadedAt < Ttl)
            return snapshot.Policy(kind);

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            snapshot = _snapshot;
            if (snapshot is not null && snapshot.BranchId == branchId && DateTime.UtcNow - snapshot.LoadedAt < Ttl)
                return snapshot.Policy(kind);

            try
            {
                var bootstrapTask = _printing.GetBootstrapAsync(branchId, _auth.DeviceId, cancellationToken);
                var routesTask = _printing.GetRoutesAsync(branchId, cancellationToken);
                var salesTask = _referenceCache.GetAsync(
                    CacheKeys.SalesPolicy,
                    _settings.GetSalesPolicyAsync);
                await Task.WhenAll(bootstrapTask, routesTask, salesTask);
                var bootstrap = await bootstrapTask;
                var routes = await routesTask;
                var sales = await salesTask;
                var policies = routes.ToDictionary(x => x.Kind);
                policies[PrintJobKind.Receipt] = bootstrap.ReceiptPolicy;
                snapshot = new Snapshot(
                    branchId,
                    DateTime.UtcNow,
                    policies,
                    sales,
                    bootstrap.DeviceNode);
                _snapshot = snapshot;
                return snapshot.Policy(kind);
            }
            catch
            {
                _snapshot = null;
                return null;
            }
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public void Invalidate() => _snapshot = null;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        Invalidate();
        if (_branch.CurrentBranchId is { } branchId)
            await GetAsync(branchId, PrintJobKind.Receipt, cancellationToken);
    }

    private void RefreshCurrent()
    {
        Invalidate();
        _ = RefreshCurrentSafeAsync();
    }

    private async Task RefreshCurrentSafeAsync()
    {
        try
        {
            await RefreshAsync();
        }
        catch
        {
            Invalidate();
        }
    }

    private void BranchChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(BranchContextService.SelectedBranch))
            RefreshCurrent();
    }

    private sealed record Snapshot(
        long BranchId,
        DateTime LoadedAt,
        IReadOnlyDictionary<PrintJobKind, PrintRoutingPolicyDto> Policies,
        SalesPolicyDto Sales,
        PrintNodeDto? DeviceNode)
    {
        public LocalPrintPolicy? Policy(PrintJobKind kind) =>
            Policies.TryGetValue(kind, out var routing)
                ? new LocalPrintPolicy(routing, Sales, HasEnabledEndpoint(kind))
                : null;

        private bool HasEnabledEndpoint(PrintJobKind kind)
        {
            var capability = kind switch
            {
                PrintJobKind.Receipt => PrintCapability.Receipt,
                PrintJobKind.BarcodeLabel => PrintCapability.BarcodeLabel,
                PrintJobKind.ZReport => PrintCapability.ZReport,
                PrintJobKind.Document => PrintCapability.Document,
                PrintJobKind.CartProforma => PrintCapability.CartProforma,
                _ => PrintCapability.None
            };
            return DeviceNode?.Endpoints.Any(
                x => x.IsEnabled && (x.Capabilities & capability) != PrintCapability.None) == true;
        }
    }
}
