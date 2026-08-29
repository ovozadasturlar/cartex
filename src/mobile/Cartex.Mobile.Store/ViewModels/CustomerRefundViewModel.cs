using System.Collections.ObjectModel;
using System.ComponentModel;
using Cartex.ApiClient.Api;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;
using Cartex.Shared.Models.Common;
using Cartex.Shared.Models.Customers;
using Cartex.Shared.Models.Rates;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Refit;

namespace Cartex.Mobile.Store.ViewModels;

public partial class CustomerRefundViewModel(
    ICustomersApi customersApi,
    ICustomerRefundsApi refundsApi,
    IRatesApi ratesApi,
    MobileAuthService auth) : ObservableObject, IQueryAttributable
{
    public ObservableCollection<CurrencyDto> Currencies { get; } = [];
    public ObservableCollection<CurrencyBalanceRow> Credits { get; } = [];
    public ObservableCollection<CheckoutPaymentRow> Tenders { get; } = [];
    public IReadOnlyList<CheckoutPaymentMethod> PaymentMethods { get; } =
    [
        new("Cash", Loc.Instance["pay_cash"]),
        new("Card", Loc.Instance["pay_card"]),
        new("Transfer", Loc.Instance["transfer"]),
        new("Bank", Loc.Instance["bank"])
    ];

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isLoaded;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string _customerName = "";
    [ObservableProperty] private string _totalText = "0";
    [ObservableProperty] private string _note = "";

    public bool CanAdd => Tenders.Count < 20;
    public bool CanRemove => Tenders.Count > 1;
    public bool HasError => !string.IsNullOrWhiteSpace(Error);

    private long _customerId;
    private readonly Dictionary<string, decimal> _available = new(StringComparer.OrdinalIgnoreCase);
    private CurrencyDto? _defaultCurrency;
    private readonly string _idempotencyKey = Guid.NewGuid().ToString("N");

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var value)) long.TryParse(value.ToString(), out _customerId);
    }

    public async Task AppearAsync()
    {
        if (IsLoaded || IsLoading || _customerId <= 0) return;
        IsLoading = true;
        Error = null;
        try
        {
            var customerTask = customersApi.GetByIdAsync(_customerId);
            var currenciesTask = ratesApi.GetCurrenciesAsync(onlyEnabled: true);
            await Task.WhenAll(customerTask, currenciesTask);
            var customer = await customerTask;
            CustomerName = customer.FullName;
            _available.Clear();
            foreach (var balance in customer.CreditBalances.Where(x => x.Amount > 0))
                _available[balance.Currency] = balance.Amount;
            if (_available.Count == 0)
            {
                Error = Loc.Instance["no_customer_credit"];
                return;
            }

            Credits.Clear();
            foreach (var balance in customer.CreditBalances.Where(x => x.Amount > 0))
                Credits.Add(new CurrencyBalanceRow(balance, true));
            Currencies.Clear();
            var allCurrencies = await currenciesTask;
            foreach (var code in _available.Keys)
            {
                var value = allCurrencies.FirstOrDefault(x => x.Code.Equals(code, StringComparison.OrdinalIgnoreCase))
                            ?? new CurrencyDto(code, code, true, true, false, false, null, null);
                Currencies.Add(value);
            }
            _defaultCurrency = Currencies.FirstOrDefault(x => x.IsBase) ?? Currencies.FirstOrDefault();
            AddTender();
            IsLoaded = true;
        }
        catch (Exception ex) { Error = Describe(ex); }
        finally { IsLoading = false; Notify(); }
    }

    [RelayCommand]
    private void AddTender()
    {
        if (!CanAdd || _defaultCurrency is null) return;
        var row = new CheckoutPaymentRow
        {
            Methods = PaymentMethods,
            CurrencyOptions = Currencies,
            SelectedMethod = PaymentMethods[0],
            SelectedCurrency = _defaultCurrency
        };
        row.PropertyChanged += OnTenderChanged;
        Tenders.Add(row);
        Recalculate();
    }

    [RelayCommand]
    private void RemoveTender(CheckoutPaymentRow row)
    {
        if (!CanRemove) return;
        row.PropertyChanged -= OnTenderChanged;
        Tenders.Remove(row);
        Recalculate();
    }

    [RelayCommand]
    private void ExactTender(CheckoutPaymentRow row)
    {
        if (row.SelectedCurrency is null) return;
        var code = row.SelectedCurrency.Code;
        var used = Tenders.Where(x => !ReferenceEquals(x, row) && x.SelectedCurrency?.Code == code).Sum(x => x.Amount);
        var remaining = Math.Max(0, _available.GetValueOrDefault(code) - used);
        var digits = Math.Clamp(row.SelectedCurrency.DecimalDigits, 0, 4);
        row.AmountText = remaining.ToString($"F{digits}");
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        var rows = Tenders.Where(x => x.Amount > 0).ToList();
        if (rows.Count == 0 || rows.Any(x => x.SelectedMethod is null || x.SelectedCurrency is null))
        {
            Error = Loc.Instance["payment_rows_invalid"];
            Notify();
            return;
        }
        foreach (var group in rows.GroupBy(x => x.SelectedCurrency!.Code, StringComparer.OrdinalIgnoreCase))
        {
            if (group.Sum(x => x.Amount) > _available.GetValueOrDefault(group.Key))
            {
                Error = string.Format(Loc.Instance["refund_exceeds_credit_fmt"], group.Key);
                Notify();
                return;
            }
        }

        IsBusy = true;
        Error = null;
        try
        {
            var result = await refundsApi.CreateAsync(new CreateCustomerRefundRequest(
                _customerId,
                auth.DefaultBranchId,
                rows.Select(x => new CustomerRefundTenderRequest(
                    x.SelectedMethod!.Code, x.SelectedCurrency!.Code, x.Amount)).ToList(),
                Note: string.IsNullOrWhiteSpace(Note) ? null : Note.Trim(),
                IdempotencyKey: _idempotencyKey));
            Ui.Toast(string.Format(Loc.Instance["customer_refund_saved_fmt"], result.DocumentNumber));
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex) { Error = Describe(ex); }
        finally { IsBusy = false; Notify(); }
    }

    private void OnTenderChanged(object? sender, PropertyChangedEventArgs e) => Recalculate();

    private void Recalculate()
    {
        var baseCode = Currencies.FirstOrDefault(x => x.IsBase)?.Code ?? "";
        TotalText = $"{Tenders.Sum(x => x.AmountBase):N2} {baseCode}";
        Notify();
    }

    private void Notify()
    {
        OnPropertyChanged(nameof(CanAdd));
        OnPropertyChanged(nameof(CanRemove));
        OnPropertyChanged(nameof(HasError));
    }

    private static string Describe(Exception exception) => exception is ApiException api
        ? ApiErrors.Describe(api)
        : Loc.Instance["err_no_connection"];
}
