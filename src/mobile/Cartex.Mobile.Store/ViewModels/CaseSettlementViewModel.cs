using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using Cartex.ApiClient.Api;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;
using Cartex.Shared.Models.Rates;
using Cartex.Shared.Models.Sales;
using Cartex.Shared.Models.TradeCases;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Refit;

namespace Cartex.Mobile.Store.ViewModels;

public partial class CaseSettlementViewModel(
    ITradeCasesApi tradeCasesApi,
    IRatesApi ratesApi) : ObservableObject, IQueryAttributable
{
    public ObservableCollection<CaseSettlementLine> Lines { get; } = [];
    public ObservableCollection<CurrencyDto> Currencies { get; } = [];
    public ObservableCollection<CheckoutPaymentRow> Payments { get; } = [];
    public IReadOnlyList<CheckoutPaymentMethod> PaymentMethods { get; } =
    [
        new("Cash", Loc.Instance["cash"]), new("Card", Loc.Instance["card"]),
        new("Transfer", Loc.Instance["transfer"]), new("Bank", Loc.Instance["bank"]),
        new("Bonus", Loc.Instance["bonus"])
    ];

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isLoaded;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string _caseTitle = "";
    [ObservableProperty] private string _customerName = "";
    [ObservableProperty] private string _totalText = "0";
    [ObservableProperty] private string _paidText = "0";
    [ObservableProperty] private string _debtText = "0";
    [ObservableProperty] private string _discountText = "";
    [ObservableProperty] private string _note = "";
    [ObservableProperty] private CurrencyDto? _selectedDebtCurrency;
    [ObservableProperty] private bool _useCustomerAdvance = true;
    [ObservableProperty] private bool _closeWhenEmpty = true;

    public bool HasSelection => Lines.Any(x => x.IsSelected);
    public bool CanAddPayment => Payments.Count < 20;
    public bool HasError => !string.IsNullOrWhiteSpace(Error);

    private long _caseId;
    private TradeCaseDetailDto? _case;
    private CurrencyDto? _baseCurrency;
    private decimal _estimatedTotal;
    private readonly string _idempotencyKey = Guid.NewGuid().ToString("N");

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var value)) long.TryParse(value.ToString(), out _caseId);
    }

    public async Task AppearAsync()
    {
        if (IsLoaded || IsLoading || _caseId <= 0) return;
        IsLoading = true;
        try
        {
            var caseTask = tradeCasesApi.GetByIdAsync(_caseId);
            var currenciesTask = ratesApi.GetCurrenciesAsync(onlyEnabled: true);
            await Task.WhenAll(caseTask, currenciesTask);
            _case = await caseTask;
            CaseTitle = $"{_case.CaseNumber} · {_case.Title}";
            CustomerName = _case.CustomerName;
            foreach (var currency in await currenciesTask) Currencies.Add(currency);
            _baseCurrency = Currencies.FirstOrDefault(x => x.IsBase) ?? Currencies.FirstOrDefault();
            SelectedDebtCurrency = _baseCurrency;
            Lines.Clear();
            foreach (var source in _case.Lines.Where(x => x.CustodyQuantity > 0))
            {
                var line = new CaseSettlementLine(source);
                line.PropertyChanged += OnLineChanged;
                Lines.Add(line);
            }
            AddPayment();
            IsLoaded = true;
            Recalculate();
        }
        catch (Exception ex) { Error = Describe(ex); }
        finally { IsLoading = false; Notify(); }
    }

    [RelayCommand]
    private void Increment(CaseSettlementLine line)
    {
        line.IsSelected = true;
        line.Quantity = Math.Min(line.Source.CustodyQuantity, line.Quantity + 1m);
    }

    [RelayCommand]
    private void Decrement(CaseSettlementLine line)
    {
        var next = Math.Max(1m, line.Quantity - 1m);
        if (next < line.Quantity) line.Quantity = next;
    }

    [RelayCommand]
    private void CommitQuantity(CaseSettlementLine line)
    {
        if (QuantityInput.TryParse(line.QuantityText, line.Source.AllowsFractional,
                out var quantity, out var error) && quantity <= line.Source.CustodyQuantity)
        {
            line.Quantity = quantity;
            line.IsSelected = true;
            return;
        }
        line.QuantityText = QuantityInput.Format(line.Quantity);
        Ui.Toast(quantity > line.Source.CustodyQuantity ? Loc.Instance["quantity_exceeds_custody"]
            : Loc.Instance[error switch
            {
                QuantityInputError.FractionNotAllowed => "quantity_integer_required",
                QuantityInputError.MustBePositive => "quantity_positive_required",
                _ => "quantity_invalid"
            }]);
    }

    [RelayCommand]
    private void AddPayment()
    {
        if (!CanAddPayment) return;
        var row = new CheckoutPaymentRow { SelectedMethod = PaymentMethods[0], SelectedCurrency = _baseCurrency };
        row.PropertyChanged += OnPaymentChanged;
        Payments.Add(row);
        Notify();
    }

    [RelayCommand]
    private void RemovePayment(CheckoutPaymentRow row)
    {
        if (Payments.Count <= 1) return;
        row.PropertyChanged -= OnPaymentChanged;
        Payments.Remove(row);
        Recalculate();
    }

    [RelayCommand]
    private void ExactPayment(CheckoutPaymentRow row)
    {
        var remaining = Math.Max(0, _estimatedTotal - Payments.Where(x => !ReferenceEquals(x, row)).Sum(x => x.AmountBase));
        if (row.EffectiveRate <= 0) { Ui.Toast(Loc.Instance["currency_rate_required"]); return; }
        var digits = Math.Clamp(row.SelectedCurrency?.DecimalDigits ?? 2, 0, 4);
        row.AmountText = Math.Round(remaining / row.EffectiveRate, digits).ToString($"F{digits}", CultureInfo.CurrentCulture);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_case is null || !HasSelection || IsBusy) return;
        var paymentRows = Payments.Where(x => x.Amount > 0).ToList();
        if (paymentRows.Any(x => x.SelectedMethod is null || x.SelectedCurrency is null || x.EffectiveRate <= 0))
        {
            Error = Loc.Instance["payment_rows_invalid"]; Notify(); return;
        }
        var discount = Parse(DiscountText);
        IsBusy = true;
        Error = null;
        try
        {
            var result = await tradeCasesApi.SettleAsync(_caseId, new SettleTradeCaseRequest(
                0, 0, 0,
                paymentRows.Select(x => new SalePaymentRequest(x.SelectedMethod!.Code, x.SelectedCurrency!.Code, x.Amount)).ToList(),
                Lines.Where(x => x.IsSelected).Select(x => new TradeCaseSettlementLineRequest(x.Source.GoodsIssueLineId, x.Quantity)).ToList(),
                DiscountAmount: discount,
                DebtCurrency: SelectedDebtCurrency?.Code,
                UseCustomerAdvance: UseCustomerAdvance,
                CloseWhenEmpty: CloseWhenEmpty,
                Note: string.IsNullOrWhiteSpace(Note) ? null : Note.Trim(),
                IdempotencyKey: _idempotencyKey,
                ExpectedCaseVersion: _case.Version));
            Ui.Toast(string.Format(Loc.Instance["settlement_created_fmt"], result.DocumentNumber));
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex) { Error = Describe(ex); }
        finally { IsBusy = false; Notify(); }
    }

    private void OnLineChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CaseSettlementLine.Quantity) or nameof(CaseSettlementLine.IsSelected)) Recalculate();
    }
    private void OnPaymentChanged(object? sender, PropertyChangedEventArgs e) => Recalculate();

    private void Recalculate()
    {
        _estimatedTotal = Lines.Where(x => x.IsSelected).Sum(x => x.Quantity * x.Source.UnitPrice);
        var paid = Payments.Sum(x => x.AmountBase);
        var debt = Math.Max(0, _estimatedTotal - Parse(DiscountText) - paid);
        TotalText = $"{_estimatedTotal:N0} {_baseCurrency?.Code}";
        PaidText = $"{paid:N0} {_baseCurrency?.Code}";
        DebtText = $"{debt:N0} {_baseCurrency?.Code}";
        Notify();
    }

    partial void OnDiscountTextChanged(string value) => Recalculate();

    private static decimal Parse(string? text) => decimal.TryParse(text?.Trim().Replace(',', '.'), NumberStyles.Number,
        CultureInfo.InvariantCulture, out var value) && value > 0 ? value : 0;

    private void Notify()
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(CanAddPayment));
        OnPropertyChanged(nameof(HasError));
    }

    private static string Describe(Exception exception) => exception is ApiException api
        ? ApiErrors.Describe(api)
        : Loc.Instance["err_no_connection"];
}

public partial class CaseSettlementLine : ObservableObject
{
    public TradeCaseLineDto Source { get; }
    public string CustodyText => $"{Source.CustodyQuantity:0.###} {Source.UnitName}";
    public decimal LineTotal => Quantity * Source.UnitPrice;

    [ObservableProperty] private bool _isSelected = true;
    [ObservableProperty] private decimal _quantity;
    [ObservableProperty] private string _quantityText;

    public CaseSettlementLine(TradeCaseLineDto source)
    {
        Source = source;
        _quantity = source.CustodyQuantity;
        _quantityText = QuantityInput.Format(_quantity);
    }

    partial void OnQuantityChanged(decimal value)
    {
        QuantityText = QuantityInput.Format(value);
        OnPropertyChanged(nameof(LineTotal));
    }
}
