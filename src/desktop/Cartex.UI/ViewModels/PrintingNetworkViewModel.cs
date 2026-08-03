using System.Collections.ObjectModel;
using Cartex.Shared.Models.Printing;
using Cartex.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.UI.ViewModels;

public sealed partial class NetworkPrintNodeItem(PrintNodeDto source) : ObservableObject
{
    public long Id => source.Id;
    public string Name => source.Name;
    public string DeviceId => source.DeviceId;
    public PrintNodeStatus Status => source.Status;
    public DateTime? LastSeenAt => source.LastSeenAt;
    public IReadOnlyList<PrinterEndpointDto> Endpoints => source.Endpoints;
    [ObservableProperty] private bool _isEnabled = source.IsEnabled;
    [ObservableProperty] private bool _isTrusted = source.IsTrusted;
}

public sealed partial class NetworkRouteEndpointItem(PrinterEndpointDto endpoint, string deviceName) : ObservableObject
{
    public long EndpointId => endpoint.Id;
    public string DeviceName => deviceName;
    public string PrinterName => endpoint.DisplayName;
    public PrintCapability Capabilities => endpoint.Capabilities;
    public PrinterEndpointStatus Status => endpoint.Status;
    public bool IsTrusted { get; init; }
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isEnabled = true;
    [ObservableProperty] private int _priority;
}

public partial class PrintingViewModel
{
    public bool CanViewPrintNetwork => _auth.HasPermission("printing.nodes.view")
        && _auth.HasPermission("printing.routes.view");
    public bool CanManagePrintNodes => _auth.HasPermission("printing.nodes.manage");
    public bool CanEditPrintRoutes => _auth.HasPermission("printing.routes.edit");
    public ObservableCollection<NetworkPrintNodeItem> NetworkNodes { get; } = [];
    public ObservableCollection<NetworkRouteEndpointItem> NetworkEndpoints { get; } = [];
    public IReadOnlyList<PrintJobKind> NetworkKinds { get; } = Enum.GetValues<PrintJobKind>();
    public IReadOnlyList<PrintRoutingMode> NetworkRoutingModes { get; } = Enum.GetValues<PrintRoutingMode>();
    public IReadOnlyList<PrintStickyMode> NetworkStickyModes { get; } = Enum.GetValues<PrintStickyMode>();
    private List<PrintRoutingPolicyDto> _networkPolicies = [];

    [ObservableProperty] private PrintJobKind _selectedNetworkKind = PrintJobKind.Receipt;
    [ObservableProperty] private PrintRoutingMode _networkRoutingMode = PrintRoutingMode.LocalFirst;
    [ObservableProperty] private PrintStickyMode _networkStickyMode = PrintStickyMode.Duration;
    [ObservableProperty] private bool _networkPolicyEnabled = true;
    [ObservableProperty] private bool _networkAllowFallback = true;
    [ObservableProperty] private bool _networkRequireTrusted = true;
    [ObservableProperty] private decimal _networkStickyMinutes = 10;
    [ObservableProperty] private decimal _networkMaxCopies = 3;
    [ObservableProperty] private decimal _networkMaxJobsPerMinute = 20;
    [ObservableProperty] private decimal _networkMaxCopiesPerMinute = 30;
    [ObservableProperty] private decimal _networkAssignmentTimeoutSeconds = 20;
    [ObservableProperty] private bool _networkAvailable;

    partial void OnSelectedNetworkKindChanged(PrintJobKind value) => ApplyNetworkPolicy();

    private async Task LoadNetworkPrintingAsync()
    {
        if (_branch.CurrentBranchId is not long branchId
            || !_auth.HasPermission("printing.nodes.view")
            || !_auth.HasPermission("printing.routes.view"))
            return;
        try
        {
            var nodes = await _printingApi.GetNodesAsync(branchId);
            _networkPolicies = await _printingApi.GetRoutesAsync(branchId);
            NetworkNodes.Clear();
            foreach (var node in nodes) NetworkNodes.Add(new NetworkPrintNodeItem(node));
            NetworkAvailable = true;
            ApplyNetworkPolicy();
        }
        catch
        {
            NetworkAvailable = false;
        }
    }

    private void ApplyNetworkPolicy()
    {
        var policy = _networkPolicies.FirstOrDefault(x => x.Kind == SelectedNetworkKind);
        if (policy is null) return;
        NetworkPolicyEnabled = policy.IsEnabled;
        NetworkRoutingMode = policy.RoutingMode;
        NetworkStickyMode = policy.StickyMode;
        NetworkAllowFallback = policy.AllowFallback;
        NetworkRequireTrusted = policy.RequireTrustedNode;
        NetworkStickyMinutes = Math.Max(0, policy.StickyDurationSeconds / 60m);
        NetworkMaxCopies = policy.MaxCopies;
        NetworkMaxJobsPerMinute = policy.MaxJobsPerMinute;
        NetworkMaxCopiesPerMinute = policy.MaxCopiesPerMinute;
        NetworkAssignmentTimeoutSeconds = policy.AssignmentTimeoutSeconds;
        var capability = CapabilityFor(SelectedNetworkKind);
        NetworkEndpoints.Clear();
        var configured = policy.Targets.ToDictionary(x => x.EndpointId);
        foreach (var node in NetworkNodes)
        foreach (var endpoint in node.Endpoints.Where(x => (x.Capabilities & capability) == capability))
        {
            configured.TryGetValue(endpoint.Id, out var target);
            NetworkEndpoints.Add(new NetworkRouteEndpointItem(endpoint, node.Name)
            {
                IsTrusted = node.IsTrusted,
                IsSelected = target is not null,
                IsEnabled = target?.IsEnabled ?? true,
                Priority = target?.Priority ?? 1000
            });
        }
        SortNetworkEndpoints();
    }

    [RelayCommand]
    private async Task SaveNetworkNodesAsync()
    {
        if (!CanManagePrintNodes) return;
        try
        {
            foreach (var node in NetworkNodes)
                await _printingApi.SetNodeAsync(node.Id, new SetPrintNodeStateRequest(node.IsEnabled, node.IsTrusted));
            await LoadNetworkPrintingAsync();
            _toast.Success(L["success"]);
        }
        catch (Exception exception)
        {
            _toast.Error(ApiErrors.Describe(exception));
        }
    }

    [RelayCommand]
    private void MoveNetworkEndpointUp(NetworkRouteEndpointItem? endpoint) => MoveNetworkEndpoint(endpoint, -1);

    [RelayCommand]
    private void MoveNetworkEndpointDown(NetworkRouteEndpointItem? endpoint) => MoveNetworkEndpoint(endpoint, 1);

    private void MoveNetworkEndpoint(NetworkRouteEndpointItem? endpoint, int offset)
    {
        if (endpoint is null) return;
        var selected = NetworkEndpoints.Where(x => x.IsSelected).OrderBy(x => x.Priority).ToList();
        var index = selected.IndexOf(endpoint);
        var target = index + offset;
        if (index < 0 || target < 0 || target >= selected.Count) return;
        (selected[index].Priority, selected[target].Priority) = (selected[target].Priority, selected[index].Priority);
        SortNetworkEndpoints();
    }

    private void SortNetworkEndpoints()
    {
        var sorted = NetworkEndpoints.OrderByDescending(x => x.IsSelected).ThenBy(x => x.Priority)
            .ThenBy(x => x.DeviceName).ThenBy(x => x.PrinterName).ToList();
        NetworkEndpoints.Clear();
        foreach (var endpoint in sorted) NetworkEndpoints.Add(endpoint);
    }

    [RelayCommand]
    private async Task SaveNetworkRouteAsync()
    {
        if (!CanEditPrintRoutes || _branch.CurrentBranchId is not long branchId) return;
        try
        {
            var targets = NetworkEndpoints.Where(x => x.IsSelected).Select((x, index) =>
                new PrintRouteTargetRequest(x.EndpointId, index + 1, x.IsEnabled)).ToList();
            var updated = await _printingApi.SetRouteAsync(SelectedNetworkKind, branchId,
                new UpdatePrintRoutingPolicyRequest(
                    NetworkPolicyEnabled,
                    NetworkRoutingMode,
                    NetworkAllowFallback,
                    NetworkStickyMode,
                    (int)Math.Clamp(NetworkStickyMinutes * 60, 0, 2592000),
                    (int)Math.Clamp(NetworkMaxCopies, 1, 100),
                    (int)Math.Clamp(NetworkMaxJobsPerMinute, 1, 1000),
                    (int)Math.Clamp(NetworkMaxCopiesPerMinute, 1, 5000),
                    (int)Math.Clamp(NetworkAssignmentTimeoutSeconds, 5, 300),
                    NetworkRequireTrusted,
                    targets));
            var index = _networkPolicies.FindIndex(x => x.Kind == updated.Kind);
            if (index >= 0) _networkPolicies[index] = updated;
            else _networkPolicies.Add(updated);
            ApplyNetworkPolicy();
            _toast.Success(L["success"]);
        }
        catch (Exception exception)
        {
            _toast.Error(ApiErrors.Describe(exception));
        }
    }

    private static PrintCapability CapabilityFor(PrintJobKind kind) => kind switch
    {
        PrintJobKind.Receipt => PrintCapability.Receipt,
        PrintJobKind.BarcodeLabel => PrintCapability.BarcodeLabel,
        PrintJobKind.ZReport => PrintCapability.ZReport,
        PrintJobKind.Document => PrintCapability.Document,
        _ => PrintCapability.None
    };
}
