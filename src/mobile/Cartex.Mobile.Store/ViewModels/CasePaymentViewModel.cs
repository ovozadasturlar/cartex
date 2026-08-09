using System.Collections.ObjectModel;
using System.ComponentModel;
using Cartex.ApiClient.Api;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;
using Cartex.Shared.Models.Customers;
using Cartex.Shared.Models.Rates;
using Cartex.Shared.Models.TradeCases;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Refit;

namespace Cartex.Mobile.Store.ViewModels;

public partial class CasePaymentViewModel(
    ITradeCasesApi tradeCasesApi,
    ICustomerPaymentsApi paymentsApi,
    IRatesApi ratesApi) : ObservableObject, IQueryAttributable
{
    public ObservableCollection<CurrencyDto> Currencies { get; } = [];
    public ObservableCollection<CheckoutPaymentRow> Payments { get; } = [];
    public IReadOnlyList<CheckoutPaymentMethod> PaymentMethods { get; } =
    [
        new("Cash", Loc.Instance["cash"]),
        new("Card", Loc.Instance["card"]),
        new("Transfer", Loc.Instance["transfer"]),
        new("Bank", Loc.Instance["bank"])
    ];

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isLoaded;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string _caseTitle = "";
    [ObservableProperty] private string _customerName = "";
    [ObservableProperty] private string _totalText = "0";
    [ObservableProperty] private string _note = "";

    public bool CanAdd => Payments.Count < 20;
    public bool CanRemove => Payments.Count > 1;
    public bool HasError => !string.IsNullOrWhiteSpace(Error);

    private long _caseId;
    private TradeCaseDetailDto? _case;
    private CurrencyDto? _baseCurrency;
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
            Currencies.Clear();
            foreach (var currency in await currenciesTask) Currencies.Add(currency);
            _baseCurrency = Currencies.FirstOrDefault(x => x.IsBase) ?? Currencies.FirstOrDefault();
            AddPayment();
            IsLoaded = true;
        }
        catch (Exception ex) { Error = Describe(ex); }
        finally { IsLoading = false; Notify(); }
    }

    [RelayCommand]
    private void AddPayment()
    {
        if (!CanAdd) return;
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
    private async Task SaveAsync()
    {
        if (_case is null || IsBusy) return;
        var rows = Payments.Where(x => x.Amount > 0).ToList();
        if (rows.Count == 0 || rows.Any(x => x.SelectedMethod is null || x.SelectedCurrency is null || x.EffectiveRate <= 0))
        {
            Error = Loc.Instance["payment_rows_invalid"];
            Notify();
            return;
        }

        IsBusy = true;
        Error = null;
        try
        {
            var result = await paymentsApi.CreateAsync(new CreateCustomerPaymentRequest(
                _case.CustomerId,
                _case.BranchId,
                rows.Select(x => new CustomerPaymentTenderRequest(
                    x.SelectedMethod!.Code, x.SelectedCurrency!.Code, x.Amount)).ToList(),
                AutoAllocateDebt: true,
                Note: string.IsNullOrWhiteSpace(Note) ? null : Note.Trim(),
                IdempotencyKey: _idempotencyKey,
                TradeCaseId: _caseId));
            Ui.Toast(result.AdvanceBaseAmount > 0
                ? Loc.Instance["payment_saved_with_advance"]
                : Loc.Instance["payment_saved"]);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex) { Error = Describe(ex); }
        finally { IsBusy = false; Notify(); }
    }

    private void OnPaymentChanged(object? sender, PropertyChangedEventArgs e) => Recalculate();

    private void Recalculate()
    {
        TotalText = $"{Payments.Sum(x => x.AmountBase):N0} {_baseCurrency?.Code ?? ""}";
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
