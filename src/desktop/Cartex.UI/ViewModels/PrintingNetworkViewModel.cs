using System.Collections.ObjectModel;
using Cartex.Shared.Models.Printing;
using Cartex.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.UI.ViewModels;

/// One row per physical device. Trust is a single switch that covers both sending print
/// requests and serving the printers attached to the device.
public sealed partial class NetworkDeviceItem : ObservableObject
{
    private bool _savedIsTrusted;

    public NetworkDeviceItem(PrintDeviceDto source)
    {
        DeviceId = source.DeviceId;
        Name = source.Name;
        NodeId = source.NodeId;
        NodeStatus = source.NodeStatus;
        HostEnabled = source.HostEnabled;
        Endpoints = source.Endpoints;
        Detail = string.Join(" · ", new[] { source.LastUsername, source.Client }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
        LastSeenText = source.LastSeenAt?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? string.Empty;
        _isTrusted = source.IsTrusted;
        _savedIsTrusted = source.IsTrusted;
    }

    public string DeviceId { get; }
    public string Name { get; }
    public long? NodeId { get; }
    public PrintNodeStatus? NodeStatus { get; }
    public bool HostEnabled { get; }
    public IReadOnlyList<PrinterEndpointDto> Endpoints { get; }
    public string Detail { get; }
    public string LastSeenText { get; }
    public bool IsHost => NodeId is not null && HostEnabled && Endpoints.Count > 0;
    public bool IsOnlineHost => IsHost && NodeStatus == PrintNodeStatus.Online;
    public string StatusText => NodeStatus?.ToString() ?? string.Empty;
    public bool HasChanges => IsTrusted != _savedIsTrusted;
    [ObservableProperty] private bool _isTrusted;

    partial void OnIsTrustedChanged(bool value) => OnPropertyChanged(nameof(HasChanges));

    public void AcceptChanges()
    {
        _savedIsTrusted = IsTrusted;
        OnPropertyChanged(nameof(HasChanges));
    }
}

public sealed partial class NetworkRouteEndpointItem(PrinterEndpointDto endpoint, string deviceName) : ObservableObject
{
    public long EndpointId => endpoint.Id;
    public string DeviceName => deviceName;
    public string PrinterName => endpoint.DisplayName;
    public PrintCapability Capabilities => endpoint.Capabilities;
    public PrinterEndpointStatus Status => endpoint.Status;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isEnabled = true;
    [ObservableProperty] private int _priority;
}

public sealed class NetworkPrintJobItem(PrintJobDto job, bool allowCancel, bool allowRetry)
{
    public long Id => job.Id;
    public string Kind => job.Kind.ToString();
    public string Status => job.Status.ToString();
    public string Summary => string.IsNullOrWhiteSpace(job.Summary) ? $"{job.SourceType} #{job.SourceId}" : job.Summary;
    public string CreatedText => job.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss");
    public string RequestedText => string.Join(" · ", new[] { job.RequestedByName, job.RequestedDeviceName, job.RequestedClient }
        .Where(x => !string.IsNullOrWhiteSpace(x)));
    public string TargetText => string.Join(" · ", new[] { job.AssignedDeviceName, job.AssignedPrinterName }
        .Where(x => !string.IsNullOrWhiteSpace(x)));
    public string CopiesText => $"×{job.Copies}";
    public string? ErrorText => string.IsNullOrWhiteSpace(job.ErrorMessage) ? job.ErrorCode : job.ErrorMessage;
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);
    public bool CanCancel => allowCancel && job.Status is PrintJobStatus.Pending or PrintJobStatus.Assigned;
    public bool CanRetry => allowRetry && job.Status is PrintJobStatus.Failed or PrintJobStatus.Cancelled;
}

public partial class PrintingViewModel
{
    public bool CanViewPrintNetwork => (_auth.HasPermission("printing.nodes.view")
        && _auth.HasPermission("printing.routes.view")) || CanViewPrintJobs;
    public bool CanManagePrintNodes => _auth.HasPermission("printing.nodes.manage");
    public bool CanEditPrintRoutes => _auth.HasPermission("printing.routes.edit");
    public bool CanViewPrintRoutes => _auth.HasPermission("printing.nodes.view") && _auth.HasPermission("printing.routes.view");
    public bool CanViewPrintJobs => _auth.HasPermission("printing.jobs.viewOwn|printing.jobs.viewBranch");
    public bool CanCancelPrintJobs => _auth.HasPermission("printing.jobs.cancel");
    public bool CanRetryPrintJobs => _auth.HasPermission("printing.jobs.retry");
    private PrintHostService? _printHost;
    private bool _isApplyingNetworkState;

    private PrintHostService PrintHost
    {
        get
        {
            if (_printHost is null)
            {
                _printHost = ServiceLocator.Resolve<PrintHostService>();
                _printHost.StatusChanged += OnPrintHostStatusChanged;
            }
            return _printHost;
        }
    }

    public string? ThisHostFailure => PrintHost.LastFailure;
    public bool HasThisHostProblem => !string.IsNullOrWhiteSpace(ThisHostFailure);

    private void OnPrintHostStatusChanged() =>
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            OnPropertyChanged(nameof(ThisHostFailure));
            OnPropertyChanged(nameof(HasThisHostProblem));
        });

    public ObservableCollection<NetworkDeviceItem> NetworkDevices { get; } = [];
    public ObservableCollection<NetworkRouteEndpointItem> NetworkEndpoints { get; } = [];
    public ObservableCollection<NetworkPrintJobItem> NetworkJobs { get; } = [];
    public IReadOnlyList<PrintRoutingMode> NetworkRoutingModes { get; } = Enum.GetValues<PrintRoutingMode>();
    public IReadOnlyList<PrintStickyMode> NetworkStickyModes { get; } = Enum.GetValues<PrintStickyMode>();
    private List<PrintRoutingPolicyDto> _networkPolicies = [];

    [ObservableProperty] private PrintJobKind _selectedNetworkKind = PrintJobKind.Receipt;
    [ObservableProperty] private PrintRoutingMode _networkRoutingMode = PrintRoutingMode.LocalFirst;
    [ObservableProperty] private PrintStickyMode _networkStickyMode = PrintStickyMode.Duration;
    [ObservableProperty] private bool _networkPolicyEnabled = true;
    [ObservableProperty] private bool _networkAllowFallback = true;
    [ObservableProperty] private decimal _networkStickyMinutes = 10;
    [ObservableProperty] private decimal _networkMaxCopies = 3;
    [ObservableProperty] private decimal _networkMaxJobsPerMinute = 20;
    [ObservableProperty] private decimal _networkMaxCopiesPerMinute = 30;
    [ObservableProperty] private decimal _networkAssignmentTimeoutSeconds = 20;
    [ObservableProperty] private bool _networkAvailable;
    [ObservableProperty] private bool _autoTrustNewDevices;

    public bool IsNetworkReceipt => SelectedNetworkKind == PrintJobKind.Receipt;
    public bool IsNetworkBarcode => SelectedNetworkKind == PrintJobKind.BarcodeLabel;
    public bool IsNetworkZReport => SelectedNetworkKind == PrintJobKind.ZReport;
    public bool IsNetworkDocument => SelectedNetworkKind == PrintJobKind.Document;
    public bool IsNetworkProforma => SelectedNetworkKind == PrintJobKind.CartProforma;

    partial void OnSelectedNetworkKindChanged(PrintJobKind value)
    {
        OnPropertyChanged(nameof(IsNetworkReceipt));
        OnPropertyChanged(nameof(IsNetworkBarcode));
        OnPropertyChanged(nameof(IsNetworkZReport));
        OnPropertyChanged(nameof(IsNetworkDocument));
        OnPropertyChanged(nameof(IsNetworkProforma));
        ApplyNetworkPolicy();
    }

    /// The switch applies straight away: it is a one-time installer decision, not part of
    /// the per-device editing flow.
    partial void OnAutoTrustNewDevicesChanged(bool value)
    {
        if (_isApplyingNetworkState || !CanManagePrintNodes || _branch.CurrentBranchId is not long branchId) return;
        _ = SetAutoTrustAsync(branchId, value);
    }

    private async Task SetAutoTrustAsync(long branchId, bool value)
    {
        try
        {
            await _printingApi.SetAutoTrustAsync(new SetPrintAutoTrustRequest(branchId, value));
            _toast.Success(L["success"]);
        }
        catch (Exception exception)
        {
            _isApplyingNetworkState = true;
            AutoTrustNewDevices = !value;
            _isApplyingNetworkState = false;
            _toast.Error(ApiErrors.Describe(exception));
        }
    }

    [RelayCommand]
    private void SelectNetworkKind(string value)
    {
        if (Enum.TryParse<PrintJobKind>(value, true, out var kind))
            SelectedNetworkKind = kind;
    }

    private async Task LoadNetworkPrintingAsync()
    {
        if (_branch.CurrentBranchId is not long branchId) return;
        try
        {
            if (_auth.HasPermission("printing.nodes.view") && _auth.HasPermission("printing.routes.view"))
            {
                var devices = await _printingApi.GetDevicesAsync(branchId);
                _networkPolicies = await _printingApi.GetRoutesAsync(branchId);
                _isApplyingNetworkState = true;
                AutoTrustNewDevices = devices.AutoTrustNewDevices;
                _isApplyingNetworkState = false;
                NetworkDevices.Clear();
                foreach (var device in devices.Devices)
                {
                    var item = new NetworkDeviceItem(device);
                    item.PropertyChanged += (_, _) => NotifyRouteHealthChanged();
                    NetworkDevices.Add(item);
                }
                ApplyNetworkPolicy();
            }
            NetworkAvailable = true;
            await LoadNetworkJobsAsync();
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
        NetworkStickyMinutes = Math.Max(0, policy.StickyDurationSeconds / 60m);
        NetworkMaxCopies = policy.MaxCopies;
        NetworkMaxJobsPerMinute = policy.MaxJobsPerMinute;
        NetworkMaxCopiesPerMinute = policy.MaxCopiesPerMinute;
        NetworkAssignmentTimeoutSeconds = policy.AssignmentTimeoutSeconds;
        var capability = CapabilityFor(SelectedNetworkKind);
        NetworkEndpoints.Clear();
        var configured = policy.Targets.ToDictionary(x => x.EndpointId);
        foreach (var device in NetworkDevices.Where(x => x.NodeId is not null))
        foreach (var endpoint in device.Endpoints.Where(x => (x.Capabilities & capability) == capability))
        {
            configured.TryGetValue(endpoint.Id, out var target);
            var item = new NetworkRouteEndpointItem(endpoint, device.Name)
            {
                IsSelected = target is not null,
                IsEnabled = target?.IsEnabled ?? true,
                Priority = target?.Priority ?? 1000
            };
            item.PropertyChanged += (_, _) => NotifyRouteHealthChanged();
            NetworkEndpoints.Add(item);
        }
        SortNetworkEndpoints();
        NotifyRouteHealthChanged();
    }

    private void NotifyRouteHealthChanged()
    {
        OnPropertyChanged(nameof(IsAutomaticRouting));
        OnPropertyChanged(nameof(HasNoRouteAndNoFallback));
        OnPropertyChanged(nameof(HasNoOnlineHostForKind));
    }

    partial void OnNetworkAllowFallbackChanged(bool value) => NotifyRouteHealthChanged();

    private bool HasSelectedRouteTarget => NetworkEndpoints.Any(x => x is { IsSelected: true, IsEnabled: true });

    /// With fallback on, an empty list is a normal setup: the system simply picks any
    /// online matching printer, so moving the printer to another computer needs no edits.
    public bool IsAutomaticRouting => !HasSelectedRouteTarget && NetworkAllowFallback;

    public bool HasNoRouteAndNoFallback => !HasSelectedRouteTarget && !NetworkAllowFallback;

    /// The one situation that really stops printing: nothing trusted is online with a
    /// matching printer right now.
    public bool HasNoOnlineHostForKind
    {
        get
        {
            var capability = CapabilityFor(SelectedNetworkKind);
            return !NetworkDevices.Any(device => device.IsTrusted && device.IsOnlineHost
                && device.Endpoints.Any(x => x.IsEnabled && (x.Capabilities & capability) == capability));
        }
    }

    [RelayCommand]
    private async Task SaveNetworkDevicesAsync()
    {
        if (!CanManagePrintNodes || _branch.CurrentBranchId is not long branchId) return;
        try
        {
            foreach (var device in NetworkDevices.Where(x => x.HasChanges))
            {
                await _printingApi.SetDeviceTrustAsync(new SetPrintDeviceTrustRequest(branchId, device.DeviceId, device.IsTrusted));
                device.AcceptChanges();
            }
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

    public void ReorderNetworkEndpoint(NetworkRouteEndpointItem? source, NetworkRouteEndpointItem? target)
    {
        if (source is null || target is null || ReferenceEquals(source, target) || !source.IsSelected || !target.IsSelected)
            return;
        var oldIndex = NetworkEndpoints.IndexOf(source);
        var newIndex = NetworkEndpoints.IndexOf(target);
        if (oldIndex < 0 || newIndex < 0) return;
        NetworkEndpoints.Move(oldIndex, newIndex);
        var priority = 1;
        foreach (var endpoint in NetworkEndpoints.Where(x => x.IsSelected))
            endpoint.Priority = priority++;
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
        PrintJobKind.CartProforma => PrintCapability.CartProforma,
        _ => PrintCapability.None
    };

    [RelayCommand]
    private Task RefreshNetworkJobsAsync() => LoadNetworkJobsAsync();

    [RelayCommand]
    private async Task CancelNetworkJobAsync(NetworkPrintJobItem? item)
    {
        if (item is null || !item.CanCancel) return;
        try
        {
            await _printingApi.CancelAsync(item.Id);
            await LoadNetworkJobsAsync();
            _toast.Success(L["success"]);
        }
        catch (Exception exception)
        {
            _toast.Error(ApiErrors.Describe(exception));
        }
    }

    [RelayCommand]
    private async Task RetryNetworkJobAsync(NetworkPrintJobItem? item)
    {
        if (item is null || !item.CanRetry) return;
        try
        {
            await _printingApi.RetryAsync(item.Id);
            await LoadNetworkJobsAsync();
            _toast.Success(L["success"]);
        }
        catch (Exception exception)
        {
            _toast.Error(ApiErrors.Describe(exception));
        }
    }

    private async Task LoadNetworkJobsAsync()
    {
        NetworkJobs.Clear();
        if (!CanViewPrintJobs || _branch.CurrentBranchId is not long branchId) return;
        try
        {
            var jobs = await _printingApi.GetJobsAsync(branchId, 50);
            foreach (var job in jobs)
                NetworkJobs.Add(new NetworkPrintJobItem(job, CanCancelPrintJobs, CanRetryPrintJobs));
        }
        catch (Exception exception)
        {
            _toast.Error(ApiErrors.Describe(exception));
        }
    }
}
