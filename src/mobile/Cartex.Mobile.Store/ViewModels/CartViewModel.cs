using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Querying;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;
using Cartex.Shared.Models.Customers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.Mobile.Store.ViewModels;

public partial class CartViewModel : ObservableObject
{
    private readonly CartStore _cart;
    private readonly ICustomersApi _customersApi;

    public ObservableCollection<CartLine> Lines { get; } = [];
    public ObservableCollection<CustomerDto> Customers { get; } = [];

    [ObservableProperty] private bool _isEmpty = true;
    [ObservableProperty] private string _totalText = "";
    [ObservableProperty] private string? _customerName;
    [ObservableProperty] private bool _hasCustomer;
    [ObservableProperty] private string _customerSearch = "";
    [ObservableProperty] private bool _hasCustomers;
    
    // Customer Modal Properties
    [ObservableProperty] private bool _isCustomerModalOpen;
    [ObservableProperty] private string _newCustomerName = "";
    [ObservableProperty] private string _newCustomerPhone = "";
    [ObservableProperty] private bool _isBusy;

    // Product Modal Properties
    [ObservableProperty] private bool _isProductModalOpen;
    [ObservableProperty] private CartLine? _selectedProduct;
    [ObservableProperty] private string _selectedProductImage = "";

    private CancellationTokenSource? _searchCts;
    private readonly ImageUrlBuilder _images;

    public CartViewModel(CartStore cart, ICustomersApi customersApi, ImageUrlBuilder images)
    {
        _cart = cart;
        _customersApi = customersApi;
        _images = images;
    }

    public void Appear()
    {
        _cart.Changed += Refresh;
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
    private void DecrementSelected()
    {
        if (SelectedProduct is not null)
            Decrement(SelectedProduct);
    }

    [RelayCommand]
    private void IncrementSelected()
    {
        if (SelectedProduct is not null)
            Increment(SelectedProduct);
    }

    [RelayCommand]
    private void Remove(CartLine line)
    {
        _cart.Remove(line.VariantId);
        if (SelectedProduct == line)
            IsProductModalOpen = false;
    }

    [RelayCommand]
    private void RemoveSelected()
    {
        if (SelectedProduct is not null)
            Remove(SelectedProduct);
    }

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
    private void OpenCustomerModal()
    {
        NewCustomerName = CustomerSearch;
        NewCustomerPhone = "";
        IsCustomerModalOpen = true;
    }

    [RelayCommand]
    private void CloseCustomerModal() => IsCustomerModalOpen = false;

    [RelayCommand]
    private async Task SaveCustomerAsync()
    {
        if (string.IsNullOrWhiteSpace(NewCustomerName) || string.IsNullOrWhiteSpace(NewCustomerPhone))
        {
            Ui.Toast(Loc.Instance["err_fill_all"]);
            return;
        }
        IsBusy = true;
        try
        {
            var request = new CreateCustomerRequest(NewCustomerName, NewCustomerPhone, null, 0);
            var id = await _customersApi.CreateAsync(request);
            _cart.SetCustomer(id, NewCustomerName);
            IsCustomerModalOpen = false;
            CustomerSearch = "";
            HasCustomers = false;
        }
        catch { Ui.Toast(Loc.Instance["err_no_connection"]); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void OpenProductModal(CartLine line)
    {
        SelectedProduct = line;
        IsProductModalOpen = true;
        SelectedProductImage = _images.FromKey(line.ImageKey) ?? "";
    }

    [RelayCommand]
    private void CloseProductModal() => IsProductModalOpen = false;

    [RelayCommand]
    private Task NextAsync()
    {
        if (_cart.Lines.Count == 0)
        {
            Ui.Toast(Loc.Instance["err_no_items"]);
            return Task.CompletedTask;
        }
        return Shell.Current.GoToAsync("checkout");
    }
}

