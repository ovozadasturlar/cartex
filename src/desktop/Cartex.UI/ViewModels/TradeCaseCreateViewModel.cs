using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Irihi.Avalonia.Shared.Contracts;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Querying;
using Cartex.Shared.Models.Customers;
using Cartex.Shared.Models.TradeCases;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class TradeCaseCreateViewModel : ViewModelBase, IDialogContext
{
    private readonly ICustomersApi _customersApi;
    private readonly ITradeCasesApi _api;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private readonly BranchContextService _branch;

    public TradeCaseCreateViewModel(ICustomersApi customersApi, ITradeCasesApi api, IToastService toast,
        IBusyService busy, BranchContextService branch)
    {
        _customersApi = customersApi;
        _api = api;
        _toast = toast;
        _busy = busy;
        _branch = branch;
    }

    public ObservableCollection<CustomerDto> CustomerResults { get; } = [];

    [ObservableProperty] private string _customerSearch = "";
    [ObservableProperty] private CustomerDto? _selectedCustomer;
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _siteAddress = "";

    public bool HasCustomer => SelectedCustomer is not null;
    public bool HasSearchResults => CustomerResults.Count > 0;
    private static readonly CustomerDto EmptyCustomer = new(0, "", null, null, null, null, null, 0, 0, 0, 0);
    public CustomerDto SelectedCustomerDisplay => SelectedCustomer ?? EmptyCustomer;

    partial void OnSelectedCustomerChanged(CustomerDto? value)
    {
        OnPropertyChanged(nameof(HasCustomer));
        OnPropertyChanged(nameof(SelectedCustomerDisplay));
    }

    private CancellationTokenSource? _customerSearchCts;

    partial void OnCustomerSearchChanged(string value)
    {
        _customerSearchCts?.Cancel();
        var cts = _customerSearchCts = new CancellationTokenSource();
        _ = DebouncedCustomerSearchAsync(cts.Token);
    }

    private async Task DebouncedCustomerSearchAsync(CancellationToken token)
    {
        try { await Task.Delay(300, token); } catch { return; }
        if (token.IsCancellationRequested) return;
        var query = CustomerSearch.Trim();
        CustomerResults.Clear();
        OnPropertyChanged(nameof(HasSearchResults));
        if (query.Length < 2) return;
        try
        {
            var result = await _customersApi.QueryAsync(QueryRequest.Create().Page(1, 20).Search(query).Build());
            if (token.IsCancellationRequested) return;
            foreach (var c in result.Content ?? []) CustomerResults.Add(c);
            OnPropertyChanged(nameof(HasSearchResults));
        }
        catch { }
    }

    [RelayCommand]
    private void PickCustomer(CustomerDto customer)
    {
        SelectedCustomer = customer;
        var fullName = $"{customer.FullName} {customer.LastName}".Trim();
        Title = $"{fullName} — {DateTime.Today:dd.MM.yyyy}";
        CustomerSearch = "";
        CustomerResults.Clear();
        OnPropertyChanged(nameof(HasSearchResults));
    }

    [RelayCommand]
    private void ClearCustomer() => SelectedCustomer = null;

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (SelectedCustomer is null || string.IsNullOrWhiteSpace(Title)) { _toast.Error(L["required_fields_hint"]); return; }
        if (_branch.CurrentWarehouseId is not { } warehouseId) { _toast.Warning(L["select_warehouse"]); return; }
        try
        {
            TradeCaseCreatedDto created;
            using (_busy.Begin(L["loading"]))
                created = await _api.CreateAsync(new CreateTradeCaseRequest(
                    SelectedCustomer.Id,
                    warehouseId,
                    Title.Trim(),
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
