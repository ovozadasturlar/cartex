using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Querying;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;
using Cartex.Shared.Models.Customers;
using Cartex.Shared.Models.Ordering;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.Mobile.Store.ViewModels;

public partial class CartViewModel : ObservableObject
{
    private readonly CartStore _cart;
    private readonly WarehouseContext _warehouse;
    private readonly IOrderingApi _orderingApi;
    private readonly ICustomersApi _customersApi;

    public ObservableCollection<CartLine> Lines { get; } = [];
    public ObservableCollection<CustomerDto> Customers { get; } = [];

    [ObservableProperty] private bool _isEmpty = true;
    [ObservableProperty] private string _totalText = "";
    [ObservableProperty] private string? _customerName;
    [ObservableProperty] private bool _hasCustomer;
    [ObservableProperty] private string _customerSearch = "";
    [ObservableProperty] private bool _hasCustomers;
    [ObservableProperty] private string _note = "";
    [ObservableProperty] private bool _isBusy;

    public bool CanSelfSell { get; }

    private CancellationTokenSource? _searchCts;
    private bool _suppressNote;

    public CartViewModel(CartStore cart, WarehouseContext warehouse, IOrderingApi orderingApi,
        ICustomersApi customersApi, MobilePermissions permissions)
    {
        _cart = cart;
        _warehouse = warehouse;
        _orderingApi = orderingApi;
        _customersApi = customersApi;
        CanSelfSell = permissions.Has("sales.create");
    }

    public void Appear()
    {
        _cart.Changed += Refresh;
        _suppressNote = true;
        Note = _cart.Note;
        _suppressNote = false;
        Refresh();
    }

    public void Disappear() => _cart.Changed -= Refresh;

    private void Refresh()
    {
        if (!Lines.SequenceEqual(_cart.Lines))
        {
            Lines.Clear();
            foreach (var line in _cart.Lines)
                Lines.Add(line);
        }
        IsEmpty = Lines.Count == 0;
        TotalText = $"{_cart.Total:N0} UZS";
        CustomerName = _cart.CustomerName;
        HasCustomer = !string.IsNullOrEmpty(_cart.CustomerName);
    }

    partial void OnNoteChanged(string value)
    {
        if (!_suppressNote) _cart.SetNote(value);
    }

    partial void OnCustomerSearchChanged(string value) => _ = SearchCustomersAsync(value);

    private async Task SearchCustomersAsync(string term)
    {
        _searchCts?.Cancel();
        var cts = _searchCts = new CancellationTokenSource();
        if (string.IsNullOrWhiteSpace(term))
        {
            Customers.Clear();
            HasCustomers = false;
            return;
        }
        try
        {
            await Task.Delay(350, cts.Token);
            var response = await _customersApi.QueryAsync(QueryRequest.Create().Page(1, 10).Search(term).Build());
            if (cts.IsCancellationRequested) return;
            Customers.Clear();
            foreach (var customer in response.Content ?? [])
                Customers.Add(customer);
            HasCustomers = Customers.Count > 0;
        }
        catch { }
    }

    [RelayCommand]
    private void Increment(CartLine line) => _cart.SetQuantity(line.VariantId, line.Quantity + 1);

    [RelayCommand]
    private void Decrement(CartLine line)
    {
        if (line.Quantity > 1) _cart.SetQuantity(line.VariantId, line.Quantity - 1);
    }

    [RelayCommand]
    private void Remove(CartLine line) => _cart.Remove(line.VariantId);

    [RelayCommand]
    private void PickCustomer(CustomerDto customer)
    {
        _cart.SetCustomer(customer.Id, customer.FullName);
        CustomerSearch = "";
        Customers.Clear();
        HasCustomers = false;
    }

    [RelayCommand]
    private void ClearCustomer() => _cart.SetCustomer(null, null);

    [RelayCommand]
    private Task QueueAsync() => SubmitAsync(false);

    [RelayCommand]
    private Task SelfSellAsync() => SubmitAsync(true);

    private async Task SubmitAsync(bool self)
    {
        if (IsBusy) return;
        if (_cart.Lines.Count == 0)
        {
            Ui.Toast(Loc.Instance["err_no_items"]);
            return;
        }
        if (!await _warehouse.EnsureSelectedAsync())
        {
            Ui.Toast(Loc.Instance["warehouse_none"]);
            return;
        }
        IsBusy = true;
        try
        {
            var request = new SubmitCartRequest(
                _warehouse.WarehouseId!.Value,
                _cart.CustomerId,
                _cart.Lines.Select(l => new SubmitCartItemRequest(l.VariantId, l.Quantity)).ToList(),
                Guid.NewGuid().ToString("N"),
                string.IsNullOrWhiteSpace(_cart.Note) ? null : _cart.Note);
            var code = await _orderingApi.SubmitAsync(request);
            _cart.Clear();
            await Shell.Current.GoToAsync(self ? $"../checkout?code={code}" : $"../handoff?code={code}");
        }
        catch (Refit.ApiException ex)
        {
            await Shell.Current.CurrentPage.DisplayAlert(Loc.Instance["error"], ApiErrors.Describe(ex), Loc.Instance["ok"]);
        }
        catch
        {
            Ui.Toast(Loc.Instance["err_no_connection"]);
        }
        finally
        {
            IsBusy = false;
        }
    }

}
