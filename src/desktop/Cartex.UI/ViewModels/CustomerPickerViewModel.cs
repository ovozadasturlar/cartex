using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Irihi.Avalonia.Shared.Contracts;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Customers;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class CustomerPickerViewModel : ViewModelBase, IDialogContext
{
    private readonly ICustomersApi _customersApi;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;

    public CustomerPickerViewModel(ICustomersApi customersApi, IToastService toast, IBusyService busy)
    {
        _customersApi = customersApi;
        _toast = toast;
        _busy = busy;
    }

    public ObservableCollection<CustomerDto> CustomerResults { get; } = [];

    [ObservableProperty] private string _customerSearch = "";
    [ObservableProperty] private bool _isCreating;
    [ObservableProperty] private string _newFirstName = "";
    [ObservableProperty] private string _newLastName = "";
    [ObservableProperty] private string _newPhone = "";

    public bool HasSearchResults => CustomerResults.Count > 0;

    private CancellationTokenSource? _customerSearchCts;

    partial void OnCustomerSearchChanged(string value)
    {
        _customerSearchCts?.Cancel();
        var cts = _customerSearchCts = new CancellationTokenSource();
        _ = DebouncedCustomerSearchAsync(cts.Token);
    }

    public async Task InitAsync()
    {
        CustomerSearch = "";
        CustomerResults.Clear();
        IsCreating = false;
        try
        {
            var list = await _customersApi.GetAllAsync();
            foreach (var c in list.Take(20)) CustomerResults.Add(c);
            OnPropertyChanged(nameof(HasSearchResults));
        }
        catch { }
    }

    private async Task DebouncedCustomerSearchAsync(CancellationToken token)
    {
        try { await Task.Delay(250, token); } catch { return; }
        if (token.IsCancellationRequested) return;
        var query = CustomerSearch.Trim();
        CustomerResults.Clear();
        OnPropertyChanged(nameof(HasSearchResults));
        try
        {
            var list = await _customersApi.GetAllAsync(string.IsNullOrWhiteSpace(query) ? null : query);
            if (token.IsCancellationRequested) return;
            foreach (var c in list.Take(20)) CustomerResults.Add(c);
            OnPropertyChanged(nameof(HasSearchResults));
        }
        catch { }
    }

    [RelayCommand]
    private void SelectCustomer(CustomerDto customer) => RequestClose?.Invoke(this, customer);

    [RelayCommand]
    private void StartCreate()
    {
        IsCreating = true;
        NewFirstName = CustomerSearch.Trim();
        NewLastName = "";
        NewPhone = "";
    }

    [RelayCommand]
    private void BackToList() => IsCreating = false;

    [RelayCommand]
    private async Task SaveAndSelectCustomerAsync()
    {
        if (string.IsNullOrWhiteSpace(NewFirstName))
        {
            _toast.Error(L["required_fields_hint"]);
            return;
        }

        try
        {
            CustomerDto created;
            using (_busy.Begin(L["loading"]))
            {
                var id = await _customersApi.CreateAsync(new CreateCustomerRequest(
                    NewFirstName.Trim(),
                    string.IsNullOrWhiteSpace(NewPhone) ? null : NewPhone.Trim(),
                    null,
                    0,
                    null,
                    string.IsNullOrWhiteSpace(NewLastName) ? null : NewLastName.Trim()));
                created = await _customersApi.GetByIdAsync(id);
            }
            RequestClose?.Invoke(this, created);
        }
        catch (Exception ex)
        {
            _toast.Error(ApiErrors.Describe(ex));
        }
    }

    public event EventHandler<object?>? RequestClose;

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(this, null);

    public void Close() => RequestClose?.Invoke(this, null);
}
