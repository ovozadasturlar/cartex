using System.Collections.ObjectModel;
using System.ComponentModel;
using Cartex.ApiClient.Api;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;
using Cartex.Shared.Models.Sales;
using Cartex.Shared.Models.Rates;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Refit;

namespace Cartex.Mobile.Store.ViewModels;

public partial class SaleReturnViewModel(
    ISalesApi salesApi,
    ICustomerReturnsApi returnsApi,
    IRatesApi ratesApi) : ObservableObject, IQueryAttributable
{
    public ObservableCollection<SaleReturnLine> Lines { get; } = [];
    public IReadOnlyList<ReturnOption> Conditions { get; } =
    [
        new("Sellable", Loc.Instance["condition_sellable"]),
        new("Opened", Loc.Instance["condition_opened"]),
        new("Damaged", Loc.Instance["condition_damaged"]),
        new("Defective", Loc.Instance["condition_defective"])
    ];
    public IReadOnlyList<ReturnOption> Dispositions { get; } =
    [
        new("SellableRestock", Loc.Instance["disposition_restock"]),
        new("Quarantine", Loc.Instance["disposition_quarantine"]),
        new("Scrap", Loc.Instance["disposition_scrap"]),
        new("SupplierClaim", Loc.Instance["disposition_supplier_claim"])
    ];
    public IReadOnlyList<ReturnOption> SettlementModes { get; } =
    [
        new("Auto", Loc.Instance["settlement_auto"]),
        new("ReduceDebt", Loc.Instance["settlement_reduce_debt"]),
        new("Cash", Loc.Instance["settlement_cash"]),
        new("Card", Loc.Instance["settlement_card"]),
        new("CustomerAdvance", Loc.Instance["settlement_advance"]),
        new("NoCharge", Loc.Instance["settlement_no_charge"])
    ];

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isLoaded;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string _customerName = "";
    [ObservableProperty] private string _saleTotalText = "";
    [ObservableProperty] private string _refundText = "0 UZS";
    [ObservableProperty] private string _note = "";
    [ObservableProperty] private ReturnOption? _selectedSettlement;

    public bool HasCustomer => !string.IsNullOrWhiteSpace(CustomerName);
    public bool HasSelection => Lines.Any(x => x.IsSelected);
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public bool IsAutomaticSettlement => SelectedSettlement?.Code == "Auto";

    private long _saleId;
    private SaleDetailDto? _sale;
    private decimal _estimatedRefund;
    private IReadOnlyList<CurrencyDto> _currencies = [];
    private string _baseCurrency = "UZS";
    private readonly string _idempotencyKey = Guid.NewGuid().ToString("N");

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var value))
            long.TryParse(value.ToString(), out _saleId);
    }

    public async Task AppearAsync()
    {
        if (IsLoaded || IsLoading || _saleId <= 0) return;
        IsLoading = true;
        Error = null;
        try
        {
            var saleTask = salesApi.GetByIdAsync(_saleId);
            var currenciesTask = ratesApi.GetCurrenciesAsync(onlyEnabled: true);
            await Task.WhenAll(saleTask, currenciesTask);
            var sale = _sale = await saleTask;
            _currencies = await currenciesTask;
            _baseCurrency = _currencies.FirstOrDefault(x => x.IsBase)?.Code ?? "UZS";
            CustomerName = sale.CustomerName ?? "";
            SaleTotalText = $"{sale.TotalAmount:N0} UZS";
            SelectedSettlement = SettlementModes[0];
            Lines.Clear();
            foreach (var item in sale.Items.Where(x => x.ReturnableQuantity > 0))
            {
                var line = new SaleReturnLine(item, Conditions[0], Dispositions[0]);
                line.PropertyChanged += OnLineChanged;
                Lines.Add(line);
            }
            IsLoaded = true;
            Recalculate();
        }
        catch (Exception ex) { Error = Describe(ex); }
        finally { IsLoading = false; NotifyState(); }
    }

    partial void OnSelectedSettlementChanged(ReturnOption? value)
    {
        OnPropertyChanged(nameof(IsAutomaticSettlement));
    }

    [RelayCommand]
    private void ToggleLine(SaleReturnLine line)
    {
        line.IsSelected = !line.IsSelected;
        if (line.IsSelected && line.Quantity <= 0)
            line.Quantity = line.Item.ReturnableQuantity;
        Recalculate();
    }

    [RelayCommand]
    private void Increment(SaleReturnLine line)
    {
        if (!line.IsSelected) line.IsSelected = true;
        line.Quantity = Math.Min(line.Item.ReturnableQuantity, line.Quantity + 1m);
        Recalculate();
    }

    [RelayCommand]
    private void Decrement(SaleReturnLine line)
    {
        var next = Math.Max(1m, line.Quantity - 1m);
        if (next < line.Quantity) line.Quantity = next;
        Recalculate();
    }

    [RelayCommand]
    private void CommitQuantity(SaleReturnLine line)
    {
        if (QuantityInput.TryParse(line.QuantityText, line.Item.AllowsFractional,
                out var quantity, out var error)
            && quantity <= line.Item.ReturnableQuantity)
        {
            line.Quantity = quantity;
            line.IsSelected = true;
            Recalculate();
            return;
        }
        line.QuantityText = QuantityInput.Format(line.Quantity);
        Ui.Toast(quantity > line.Item.ReturnableQuantity
            ? Loc.Instance["return_quantity_exceeded"]
            : Loc.Instance[error switch
            {
                QuantityInputError.FractionNotAllowed => "quantity_integer_required",
                QuantityInputError.MustBePositive => "quantity_positive_required",
                _ => "quantity_invalid"
            }]);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_sale is null || IsBusy || !HasSelection) return;
        foreach (var line in Lines.Where(x => x.IsSelected))
            CommitQuantity(line);

        var selected = Lines.Where(x => x.IsSelected).ToList();
        if (selected.Count == 0) return;
        IsBusy = true;
        Error = null;
        try
        {
            var mode = SelectedSettlement?.Code ?? "Auto";
            List<CustomerReturnSettlementRequest>? settlements = null;
            if (mode != "Auto")
            {
                var currencyCode = mode == "ReduceDebt" && !string.IsNullOrWhiteSpace(_sale.DebtCurrency)
                    ? _sale.DebtCurrency
                    : _baseCurrency;
                var currency = _currencies.FirstOrDefault(x => string.Equals(x.Code, currencyCode, StringComparison.OrdinalIgnoreCase));
                var rate = currency?.IsBase == true ? 1m : currency?.Rate ?? 0;
                if (rate <= 0)
                    throw new InvalidOperationException(Loc.Instance["currency_rate_required"]);
                var amount = Math.Round(_estimatedRefund / rate, Math.Clamp(currency?.DecimalDigits ?? 2, 0, 4));
                settlements = [new CustomerReturnSettlementRequest(mode, currencyCode, amount)];
            }
            var result = await returnsApi.CreateAsync(new CreateCustomerReturnRequest(
                _sale.Id,
                selected.Select(x => new CustomerReturnLineRequest(
                    x.Item.SaleItemId,
                    x.Quantity,
                    string.IsNullOrWhiteSpace(x.Reason) ? null : x.Reason.Trim(),
                    x.SelectedCondition?.Code ?? "Sellable",
                    x.SelectedDisposition?.Code ?? "SellableRestock")).ToList(),
                settlements,
                AutoSettle: mode == "Auto",
                Note: string.IsNullOrWhiteSpace(Note) ? null : Note.Trim(),
                IdempotencyKey: _idempotencyKey));
            Ui.Toast(string.Format(Loc.Instance["return_created_fmt"], result.DocumentNumber));
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            Error = Describe(ex);
        }
        finally
        {
            IsBusy = false;
            NotifyState();
        }
    }

    private void OnLineChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SaleReturnLine.Quantity) or nameof(SaleReturnLine.IsSelected))
            Recalculate();
    }

    private void Recalculate()
    {
        if (_sale is null) return;
        var gross = _sale.Items.Sum(x => x.Quantity * x.UnitPrice);
        var discountRate = gross > 0 ? _sale.DiscountAmount / gross : 0;
        _estimatedRefund = Math.Round(Lines.Where(x => x.IsSelected)
            .Sum(x => x.Quantity * x.Item.UnitPrice) * (1 - discountRate), 2);
        RefundText = $"{_estimatedRefund:N0} UZS";
        NotifyState();
    }

    private void NotifyState()
    {
        OnPropertyChanged(nameof(HasCustomer));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(HasError));
    }

    private static string Describe(Exception exception) => exception is ApiException api
        ? ApiErrors.Describe(api)
        : Loc.Instance["err_no_connection"];
}

public sealed record ReturnOption(string Code, string Label);

public partial class SaleReturnLine : ObservableObject
{
    public SaleDetailItemDto Item { get; }

    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private decimal _quantity;
    [ObservableProperty] private string _quantityText;
    [ObservableProperty] private string _reason = "";
    [ObservableProperty] private ReturnOption? _selectedCondition;
    [ObservableProperty] private ReturnOption? _selectedDisposition;

    public SaleReturnLine(SaleDetailItemDto item, ReturnOption condition, ReturnOption disposition)
    {
        Item = item;
        _quantity = item.ReturnableQuantity;
        _quantityText = QuantityInput.Format(_quantity);
        _selectedCondition = condition;
        _selectedDisposition = disposition;
    }

    partial void OnQuantityChanged(decimal value) => QuantityText = QuantityInput.Format(value);
}
