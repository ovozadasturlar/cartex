using System.Collections.ObjectModel;
using Cartex.Mobile.Agent.Data;
using Cartex.Mobile.Agent.Models;
using Cartex.Mobile.Agent.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.Mobile.Core;

namespace Cartex.Mobile.Agent.ViewModels;

public partial class SaleViewModel(AgentDb db, SyncService sync) : ObservableObject, IQueryAttributable
{
    public ObservableCollection<SaleLine> Lines { get; } = [];
    public ObservableCollection<LocalVanStock> Results { get; } = [];

    [ObservableProperty] private string _customerName = Loc.Instance["cash_customer"];
    [ObservableProperty] private string _search = "";
    [ObservableProperty] private string _paidCashText = "";
    [ObservableProperty] private string _totalText = "";
    [ObservableProperty] private string? _debtInfo;
    [ObservableProperty] private string? _creditWarning;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private bool _hasResults;
    [ObservableProperty] private bool _hasLines;

    private long? _customerId;
    private LocalCustomer? _customer;
    private string _currency = "";
    private long _warehouseId;
    private bool _paidEdited;
    private bool _suppressPaidEdit;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("customerId", out var id))
            _customerId = (long)id;
    }

    public async Task AppearAsync()
    {
        _currency = await db.GetMetaAsync("base_currency") ?? "";
        _warehouseId = long.TryParse(await db.GetMetaAsync("warehouse_id"), out var w) ? w : 0;
        if (_customerId is { } cid)
        {
            _customer = await db.GetCustomerAsync(cid);
            CustomerName = _customer?.FullName ?? Loc.Instance["cash_customer"];
        }
        Recalc();
    }

    partial void OnSearchChanged(string value) => _ = SearchStockAsync(value);

    partial void OnPaidCashTextChanged(string value)
    {
        if (!_suppressPaidEdit) _paidEdited = true;
        Recalc();
    }

    private async Task SearchStockAsync(string term)
    {
        Results.Clear();
        if (string.IsNullOrWhiteSpace(term))
        {
            HasResults = false;
            return;
        }
        foreach (var s in (await db.SearchVanStockAsync(term)).Take(15))
            Results.Add(s);
        HasResults = Results.Count > 0;
    }

    [RelayCommand]
    private void AddItem(LocalVanStock stock)
    {
        var line = Lines.FirstOrDefault(l => l.VariantId == stock.VariantId);
        if (line is null)
        {
            line = new SaleLine(this)
            {
                VariantId = stock.VariantId,
                Name = stock.ProductName,
                UnitName = stock.UnitName,
                UnitPrice = stock.SellingPrice,
                Available = stock.Quantity
            };
            Lines.Add(line);
        }
        line.Quantity += 1;
        Search = "";
        Results.Clear();
        HasResults = false;
        Recalc();
    }

    [RelayCommand]
    private void RemoveLine(SaleLine line)
    {
        Lines.Remove(line);
        Recalc();
    }

    public void Recalc()
    {
        HasLines = Lines.Count > 0;
        var total = Lines.Sum(l => l.Total);
        TotalText = $"{total:N0} {_currency}";
        if (!_paidEdited)
        {
            _suppressPaidEdit = true;
            PaidCashText = total.ToString("0");
            _suppressPaidEdit = false;
        }

        var paid = ParsePaid();
        var debt = total - paid;
        DebtInfo = debt > 0 ? string.Format(Loc.Instance["debt_info_fmt"], debt, _currency) : null;
        CreditWarning = debt > 0 && _customer is { CreditLimit: > 0 } c && c.DebtBalance + debt > c.CreditLimit
            ? Loc.Instance["credit_warning"]
            : null;
        foreach (var line in Lines)
            line.RefreshOverStock();
    }

    private decimal ParsePaid() =>
        decimal.TryParse(PaidCashText.Replace(" ", ""), out var v) && v >= 0 ? v : 0;

    [RelayCommand]
    private void FullCash()
    {
        _paidEdited = false;
        Recalc();
    }

    [RelayCommand]
    private void FullDebt()
    {
        _paidEdited = true;
        PaidCashText = "0";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        Error = null;
        if (Lines.Count == 0) { Error = Loc.Instance["err_no_items"]; return; }
        if (_warehouseId == 0) { Error = Loc.Instance["err_no_warehouse"]; return; }
        var total = Lines.Sum(l => l.Total);
        var paid = ParsePaid();
        if (paid > total) { Error = Loc.Instance["err_paid_gt_total"]; return; }
        if (paid < total && _customerId is null) { Error = Loc.Instance["err_debt_needs_customer"]; return; }

        var draft = new SaleDraft(_warehouseId, _customerId, _customer?.FullName, paid,
            Lines.Select(l => new SaleDraftItem(l.VariantId, l.Name, l.Quantity, l.UnitPrice)).ToList());
        await sync.EnqueueSaleAsync(draft);
        Ui.Toast(Loc.Instance["sale_queued"]);
        await Shell.Current.GoToAsync("..");
    }
}

public partial class SaleLine(SaleViewModel owner) : ObservableObject
{
    public long VariantId { get; init; }
    public string Name { get; init; } = "";
    public string UnitName { get; init; } = "";
    public decimal UnitPrice { get; init; }
    public decimal Available { get; init; }

    [ObservableProperty] private decimal _quantity;
    [ObservableProperty] private bool _isOverStock;

    public decimal Total => Quantity * UnitPrice;
    public string PriceText => $"{Quantity:0.###} {UnitName} × {UnitPrice:N0} = {Total:N0}";

    partial void OnQuantityChanged(decimal value)
    {
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(PriceText));
        owner.Recalc();
    }

    public void RefreshOverStock() => IsOverStock = Quantity > Available;

    [RelayCommand]
    private void Increment() => Quantity += 1;

    [RelayCommand]
    private void Decrement()
    {
        if (Quantity > 1) Quantity -= 1;
    }
}
