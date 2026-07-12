using System.Collections.ObjectModel;
using Cartex.Mobile.Agent.Data;
using Cartex.Mobile.Agent.Models;
using Cartex.Mobile.Agent.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.Mobile.Core;

namespace Cartex.Mobile.Agent.ViewModels;

public partial class OrderViewModel(AgentDb db, SyncService sync) : ObservableObject
{
    public ObservableCollection<OrderLine> Lines { get; } = [];
    public ObservableCollection<LocalVanStock> ProductResults { get; } = [];
    public ObservableCollection<LocalCustomer> CustomerResults { get; } = [];

    [ObservableProperty] private string _customerSearch = "";
    [ObservableProperty] private string _productSearch = "";
    [ObservableProperty] private string? _selectedCustomerName;
    [ObservableProperty] private string _totalText = "";
    [ObservableProperty] private string? _error;
    [ObservableProperty] private bool _hasCustomerResults;
    [ObservableProperty] private bool _hasProductResults;
    [ObservableProperty] private bool _hasCustomer;
    [ObservableProperty] private bool _hasLines;

    private long? _customerId;
    private string _currency = "";
    private long _warehouseId;

    public async Task AppearAsync()
    {
        _currency = await db.GetMetaAsync("base_currency") ?? "";
        _warehouseId = long.TryParse(await db.GetMetaAsync("warehouse_id"), out var w) ? w : 0;
    }

    partial void OnCustomerSearchChanged(string value) => _ = SearchCustomersAsync(value);
    partial void OnProductSearchChanged(string value) => _ = SearchProductsAsync(value);

    private async Task SearchCustomersAsync(string term)
    {
        CustomerResults.Clear();
        if (string.IsNullOrWhiteSpace(term) || _customerId is not null)
        {
            HasCustomerResults = false;
            return;
        }
        foreach (var c in (await db.SearchCustomersAsync(term)).Take(12))
            CustomerResults.Add(c);
        HasCustomerResults = CustomerResults.Count > 0;
    }

    [RelayCommand]
    private Task AddCustomerAsync() => Shell.Current.GoToAsync("customer-new");

    [RelayCommand]
    private void SelectCustomer(LocalCustomer customer)
    {
        _customerId = customer.Id;
        SelectedCustomerName = customer.FullName;
        HasCustomer = true;
        CustomerSearch = "";
        CustomerResults.Clear();
        HasCustomerResults = false;
    }

    [RelayCommand]
    private void ClearCustomer()
    {
        _customerId = null;
        SelectedCustomerName = null;
        HasCustomer = false;
    }

    private async Task SearchProductsAsync(string term)
    {
        ProductResults.Clear();
        if (string.IsNullOrWhiteSpace(term))
        {
            HasProductResults = false;
            return;
        }
        foreach (var s in (await db.SearchVanStockAsync(term)).Take(15))
            ProductResults.Add(s);
        HasProductResults = ProductResults.Count > 0;
    }

    [RelayCommand]
    private void AddItem(LocalVanStock stock)
    {
        var line = Lines.FirstOrDefault(l => l.VariantId == stock.VariantId);
        if (line is null)
        {
            line = new OrderLine(this)
            {
                VariantId = stock.VariantId,
                Name = stock.ProductName,
                UnitName = stock.UnitName,
                UnitPrice = stock.SellingPrice
            };
            Lines.Add(line);
        }
        line.Quantity += 1;
        ProductSearch = "";
        ProductResults.Clear();
        HasProductResults = false;
        Recalc();
    }

    [RelayCommand]
    private void RemoveLine(OrderLine line)
    {
        Lines.Remove(line);
        Recalc();
    }

    public void Recalc()
    {
        HasLines = Lines.Count > 0;
        TotalText = $"{Lines.Sum(l => l.Total):N0} {_currency}";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        Error = null;
        if (_customerId is null) { Error = Loc.Instance["err_order_needs_customer"]; return; }
        if (Lines.Count == 0) { Error = Loc.Instance["err_no_items"]; return; }
        if (_warehouseId == 0) { Error = Loc.Instance["err_no_warehouse"]; return; }

        var draft = new OrderDraft(Guid.NewGuid().ToString("N"), _warehouseId, _customerId, SelectedCustomerName,
            Lines.Select(l => new OrderDraftItem(l.VariantId, l.Name, l.UnitName, l.Quantity, l.UnitPrice)).ToList());
        await sync.EnqueueOrderAsync(draft);
        Ui.Toast(Loc.Instance["order_queued"]);
        await Shell.Current.GoToAsync("..");
    }
}

public partial class OrderLine(OrderViewModel owner) : ObservableObject
{
    public long VariantId { get; init; }
    public string Name { get; init; } = "";
    public string UnitName { get; init; } = "";
    public decimal UnitPrice { get; init; }

    [ObservableProperty] private decimal _quantity;

    public decimal Total => Quantity * UnitPrice;
    public string PriceText => $"{Quantity:0.###} {UnitName} × {UnitPrice:N0} = {Total:N0}";

    partial void OnQuantityChanged(decimal value)
    {
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(PriceText));
        owner.Recalc();
    }

    [RelayCommand]
    private void Increment() => Quantity += 1;

    [RelayCommand]
    private void Decrement()
    {
        if (Quantity > 1) Quantity -= 1;
    }
}
