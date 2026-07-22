using System.Collections.ObjectModel;
using Cartex.Mobile.Agent.Data;
using Cartex.Mobile.Agent.Models;
using Cartex.Mobile.Agent.Services;
using Cartex.Mobile.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.Mobile.Agent.ViewModels;

public partial class CartViewModel(AgentDb db, SyncService sync, CartService cart, AppCapabilities caps) : ObservableObject
{
    private long _warehouseId;
    private LocalCustomer? _customer;
    private bool _paidEdited;
    private bool _suppressPaidEdit;

    public ObservableCollection<LocalCustomer> CustomerResults { get; } = [];

    [ObservableProperty] private string _paidText = "";
    [ObservableProperty] private string _customerSearch = "";
    [ObservableProperty] private bool _isPickingCustomer;
    [ObservableProperty] private string? _error;

    public CartService Cart => cart;
    public bool CanOverridePrice => caps.CanOverridePrice;

    public string CustomerLabel => _customer?.FullName ?? Loc.Instance["cash_customer"];
    public decimal Paid => Money.Parse(PaidText);
    public decimal Debt => Math.Max(0, cart.Total - Paid);
    public string DebtText => Money.Text(Debt, cart.Currency);
    public bool HasDebt => Debt > 0;
    public bool CreditExceeded =>
        HasDebt && _customer is { CreditLimit: > 0 } c && c.DebtBalance + Debt > c.CreditLimit;

    public async Task AppearAsync()
    {
        cart.Currency = await db.GetMetaAsync("base_currency") ?? "";
        _warehouseId = long.TryParse(await db.GetMetaAsync("warehouse_id"), out var w) ? w : 0;
        cart.PropertyChanged += OnCartChanged;
        Recalc();
    }

    public void Disappear() => cart.PropertyChanged -= OnCartChanged;

    private void OnCartChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => Recalc();

    partial void OnPaidTextChanged(string value)
    {
        if (!_suppressPaidEdit) _paidEdited = true;
        RaiseTotals();
    }

    partial void OnCustomerSearchChanged(string value) => _ = SearchCustomersAsync(value);

    private void Recalc()
    {
        if (!_paidEdited)
        {
            _suppressPaidEdit = true;
            PaidText = cart.Total.ToString("0");
            _suppressPaidEdit = false;
        }
        RaiseTotals();
    }

    private void RaiseTotals()
    {
        OnPropertyChanged(nameof(Paid));
        OnPropertyChanged(nameof(Debt));
        OnPropertyChanged(nameof(DebtText));
        OnPropertyChanged(nameof(HasDebt));
        OnPropertyChanged(nameof(CreditExceeded));
    }

    private async Task SearchCustomersAsync(string term)
    {
        CustomerResults.Clear();
        foreach (var c in (await db.SearchCustomersAsync(term)).Take(30))
            CustomerResults.Add(c);
    }

    [RelayCommand]
    private void Remove(CartLine line) => cart.Remove(line);

    [RelayCommand]
    private async Task ContinueAsync() => await Shell.Current.GoToAsync("//catalog");

    [RelayCommand]
    private void Clear()
    {
        cart.Clear();
        _customer = null;
        _paidEdited = false;
        OnPropertyChanged(nameof(CustomerLabel));
        Recalc();
    }

    [RelayCommand]
    private async Task PickCustomerAsync()
    {
        CustomerSearch = "";
        await SearchCustomersAsync("");
        IsPickingCustomer = true;
    }

    [RelayCommand]
    private void ChooseCustomer(LocalCustomer? customer)
    {
        _customer = customer;
        cart.CustomerId = customer?.Id;
        cart.CustomerName = customer?.FullName;
        IsPickingCustomer = false;
        OnPropertyChanged(nameof(CustomerLabel));
        RaiseTotals();
    }

    [RelayCommand]
    private void CancelPick() => IsPickingCustomer = false;

    [RelayCommand]
    private void FullPaid()
    {
        _paidEdited = false;
        Recalc();
    }

    [RelayCommand]
    private void FullDebt()
    {
        _paidEdited = true;
        PaidText = "0";
    }

    [RelayCommand]
    private async Task CheckoutAsync()
    {
        Error = null;
        if (cart.Lines.Count == 0) { Error = Loc.Instance["err_no_items"]; return; }
        if (_warehouseId == 0) { Error = Loc.Instance["err_no_warehouse"]; return; }
        if (Paid > cart.Total) { Error = Loc.Instance["err_paid_gt_total"]; return; }
        if (HasDebt && _customer is null) { Error = Loc.Instance["err_debt_needs_customer"]; return; }

        var draft = new SaleDraft(_warehouseId, _customer?.Id, _customer?.FullName, Paid,
            [.. cart.Lines.Select(l => new SaleDraftItem(l.VariantId, l.Name, l.Quantity, l.UnitPrice))]);

        await sync.EnqueueSaleAsync(draft);
        Clear();
        Ui.Haptic();
        Ui.Toast(Loc.Instance["sale_queued"]);
        await Shell.Current.GoToAsync("//catalog");
    }
}
