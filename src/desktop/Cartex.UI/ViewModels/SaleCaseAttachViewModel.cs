using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Irihi.Avalonia.Shared.Contracts;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.TradeCases;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class SaleCaseAttachViewModel : ViewModelBase, IDialogContext
{
    private readonly ITradeCasesApi _api;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private readonly BranchContextService _branch;

    private long _customerId;

    public SaleCaseAttachViewModel(ITradeCasesApi api, IToastService toast, IBusyService busy, BranchContextService branch)
    {
        _api = api;
        _toast = toast;
        _busy = busy;
        _branch = branch;
    }

    public ObservableCollection<TradeCaseListDto> OpenCases { get; } = [];

    [ObservableProperty] private bool _isCreating;
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _siteAddress = "";
    [ObservableProperty] private bool _hasOpenCases;
    [ObservableProperty] private string _customerDisplay = "";

    public async Task InitAsync(long customerId, string customerName)
    {
        _customerId = customerId;
        CustomerDisplay = customerName;
        Title = $"{customerName} — {DateTime.Today:dd.MM.yyyy}";
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        OpenCases.Clear();
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                var open = await _api.GetAsync(customerId: _customerId, status: "Open", pageSize: 50);
                var pending = await _api.GetAsync(customerId: _customerId, status: "SettlementPending", pageSize: 50);
                foreach (var c in open.Concat(pending).OrderByDescending(x => x.UpdatedAt)) OpenCases.Add(c);
            }
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
        HasOpenCases = OpenCases.Count > 0;
        if (!HasOpenCases) IsCreating = true;
    }

    [RelayCommand]
    private void StartCreate() => IsCreating = true;

    [RelayCommand]
    private void BackToList() => IsCreating = false;

    [RelayCommand]
    private void SelectCase(TradeCaseListDto item) => RequestClose?.Invoke(this, item);

    [RelayCommand]
    private async Task CreateAndPickAsync()
    {
        if (string.IsNullOrWhiteSpace(Title)) { _toast.Error(L["required_fields_hint"]); return; }
        if (_branch.CurrentWarehouseId is not { } warehouseId) { _toast.Warning(L["select_warehouse"]); return; }
        try
        {
            TradeCaseCreatedDto created;
            using (_busy.Begin(L["loading"]))
                created = await _api.CreateAsync(new CreateTradeCaseRequest(
                    _customerId, warehouseId, Title.Trim(),
                    string.IsNullOrWhiteSpace(SiteAddress) ? null : SiteAddress.Trim(),
                    IdempotencyKey: Guid.NewGuid().ToString("N")));
            RequestClose?.Invoke(this, created);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    public event EventHandler<object?>? RequestClose;

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(this, null);

    public void Close() => RequestClose?.Invoke(this, null);
}
