using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Querying;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;
using Cartex.Shared.Models.Common;
using Cartex.Shared.Models.Customers;
using Cartex.Shared.Models.Rates;
using Cartex.Shared.Models.Sales;
using Cartex.Shared.Models.TradeCases;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Refit;

namespace Cartex.Mobile.Store.ViewModels;

public partial class CustomerDetailViewModel(
    ICustomersApi customersApi,
    ISalesApi salesApi,
    ITradeCasesApi tradeCasesApi,
    ICustomerPaymentsApi customerPaymentsApi,
    ICustomerRefundsApi customerRefundsApi,
    ICustomerReturnsApi customerReturnsApi,
    IRatesApi ratesApi,
    MobilePermissions permissions,
    MobileAuthService auth) : ObservableObject, IQueryAttributable
{
    public ObservableCollection<CurrencyBalanceRow> Debts { get; } = [];
    public ObservableCollection<CurrencyBalanceRow> Credits { get; } = [];
    public ObservableCollection<CustomerLedgerEntryDto> Ledger { get; } = [];
    public ObservableCollection<CustomerSaleRow> Sales { get; } = [];
    public ObservableCollection<TradeCaseListDto> Cases { get; } = [];
    public ObservableCollection<CustomerPaymentListDto> Payments { get; } = [];
    public ObservableCollection<CustomerReturnListDto> Returns { get; } = [];
    public ObservableCollection<CustomerRefundListDto> Refunds { get; } = [];
    public ObservableCollection<CurrencyDto> Currencies { get; } = [];

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isLoaded;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private CustomerDto? _customer;
    [ObservableProperty] private string _initials = "";
    [ObservableProperty] private string _selectedTab = "overview";
    [ObservableProperty] private bool _isPaymentOpen;
    [ObservableProperty] private string _paymentAmount = "";
    [ObservableProperty] private string _paymentNote = "";
    [ObservableProperty] private string _paymentMethod = "Cash";
    [ObservableProperty] private CurrencyDto? _selectedCurrency;
    [ObservableProperty] private bool _canReceivePayment;
    [ObservableProperty] private bool _canMessage;
    [ObservableProperty] private bool _canCreateCase;
    [ObservableProperty] private bool _canRefund;
    [ObservableProperty] private bool _canViewStatement;

    public bool IsOverview => SelectedTab == "overview";
    public bool IsCases => SelectedTab == "cases";
    public bool IsTimeline => SelectedTab == "timeline";
    public bool IsSales => SelectedTab == "sales";
    public bool IsFinance => SelectedTab == "finance";
    public bool HasPhone => !string.IsNullOrWhiteSpace(Customer?.Phone);
    public bool HasEmail => !string.IsNullOrWhiteSpace(Customer?.Email);
    public bool HasAddress => !string.IsNullOrWhiteSpace(Customer?.Address);
    public bool HasDebts => Debts.Count > 0;
    public bool HasCredits => Credits.Count > 0;
    public bool HasCases => Cases.Count > 0;
    public bool HasLedger => Ledger.Count > 0;
    public bool HasSales => Sales.Count > 0;
    public bool HasPayments => Payments.Count > 0;
    public bool HasReturns => Returns.Count > 0;
    public bool HasRefunds => Refunds.Count > 0;
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public bool IsCash => PaymentMethod == "Cash";
    public bool IsCard => PaymentMethod == "Card";

    private long _customerId;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var value))
            long.TryParse(value.ToString(), out _customerId);
    }

    public async Task AppearAsync()
    {
        if (!IsLoading)
            await LoadAsync();
    }

    partial void OnSelectedTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsOverview));
        OnPropertyChanged(nameof(IsCases));
        OnPropertyChanged(nameof(IsTimeline));
        OnPropertyChanged(nameof(IsSales));
        OnPropertyChanged(nameof(IsFinance));
    }

    partial void OnPaymentMethodChanged(string value)
    {
        OnPropertyChanged(nameof(IsCash));
        OnPropertyChanged(nameof(IsCard));
    }

    [RelayCommand]
    private void SelectTab(string tab) => SelectedTab = tab;

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    private void Call()
    {
        if (!HasPhone) return;
        try { PhoneDialer.Default.Open(Customer!.Phone!); }
        catch { Ui.Toast(Loc.Instance["phone_action_failed"]); }
    }

    [RelayCommand]
    private async Task SmsAsync()
    {
        if (!HasPhone) return;
        try { await Sms.Default.ComposeAsync(new SmsMessage("", [Customer!.Phone!])); }
        catch { Ui.Toast(Loc.Instance["phone_action_failed"]); }
    }

    [RelayCommand]
    private async Task MessageAsync()
    {
        if (Customer is null || !CanMessage) return;
        var text = await Shell.Current.CurrentPage.DisplayPromptAsync(
            Loc.Instance["send_message"], Loc.Instance["message_text"],
            Loc.Instance["send"], Loc.Instance["cancel"]);
        if (string.IsNullOrWhiteSpace(text)) return;

        IsBusy = true;
        try
        {
            await customersApi.SendMessageAsync(Customer.Id, new SendCustomerMessageRequest("auto", text.Trim()));
            Ui.Toast(Loc.Instance["message_sent"]);
        }
        catch (Exception ex) { Ui.Toast(Describe(ex)); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private Task OpenSaleAsync(CustomerSaleRow row) => Shell.Current.GoToAsync($"sale/detail?id={row.Sale.Id}");

    [RelayCommand]
    private Task OpenCaseAsync(TradeCaseListDto row) => Shell.Current.GoToAsync($"case/detail?id={row.Id}");

    [RelayCommand]
    private Task CreateCaseAsync() => CanCreateCase
        ? Shell.Current.GoToAsync($"case/create?customerId={_customerId}")
        : Task.CompletedTask;

    [RelayCommand]
    private Task OpenStatementAsync() => CanViewStatement
        ? Shell.Current.GoToAsync($"customer/statement?id={_customerId}")
        : Task.CompletedTask;

    [RelayCommand]
    private Task OpenRefundAsync() => CanRefund
        ? Shell.Current.GoToAsync($"customer/refund?id={_customerId}")
        : Task.CompletedTask;

    [RelayCommand]
    private void OpenPayment()
    {
        if (!CanReceivePayment || Customer is null) return;
        PaymentAmount = "";
        PaymentNote = "";
        PaymentMethod = "Cash";
        SelectedCurrency = Currencies.FirstOrDefault(x => x.IsBase) ?? Currencies.FirstOrDefault();
        IsPaymentOpen = true;
    }

    [RelayCommand]
    private void ClosePayment() => IsPaymentOpen = false;

    [RelayCommand]
    private void SelectPaymentMethod(string method) => PaymentMethod = method;

    [RelayCommand]
    private async Task SavePaymentAsync()
    {
        if (Customer is null || SelectedCurrency is null || IsBusy) return;
        if (!decimal.TryParse(PaymentAmount.Trim().Replace(',', '.'),
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out var amount) || amount <= 0)
        {
            Error = Loc.Instance["amount_invalid"];
            return;
        }

        IsBusy = true;
        Error = null;
        try
        {
            var result = await customerPaymentsApi.CreateAsync(new CreateCustomerPaymentRequest(
                Customer.Id,
                auth.DefaultBranchId,
                [new CustomerPaymentTenderRequest(PaymentMethod, SelectedCurrency.Code, amount)],
                AutoAllocateDebt: true,
                Note: string.IsNullOrWhiteSpace(PaymentNote) ? null : PaymentNote.Trim(),
                IdempotencyKey: Guid.NewGuid().ToString("N")));
            IsPaymentOpen = false;
            Ui.Toast(result.AdvanceBaseAmount > 0
                ? Loc.Instance["payment_saved_with_advance"]
                : Loc.Instance["payment_saved"]);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            Error = Describe(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadAsync()
    {
        if (_customerId <= 0 || IsLoading) return;
        IsLoading = true;
        Error = null;

        try
        {
            CanReceivePayment = permissions.Has("customer_payments.create");
            CanMessage = permissions.Has("customers.message");
            CanCreateCase = permissions.Has("trade_cases.create");
            CanViewStatement = permissions.Has("statements.view");

            var customerTask = customersApi.GetByIdAsync(_customerId);
            var ledgerTask = customersApi.GetLedgerAsync(_customerId, 1, 50);
            var salesTask = salesApi.QueryAsync(QueryRequest.Create().Page(1, 30).Sort("CreatedAt", true)
                .With("customerId", _customerId).Build());
            var casesTask = permissions.Has("trade_cases.view")
                ? tradeCasesApi.GetAsync(customerId: _customerId, page: 1, pageSize: 30)
                : Task.FromResult(new List<TradeCaseListDto>());
            var paymentsTask = permissions.Has("customer_payments.view")
                ? customerPaymentsApi.GetAsync(customerId: _customerId, page: 1, pageSize: 30)
                : Task.FromResult(new List<CustomerPaymentListDto>());
            var returnsTask = permissions.Has("returns.view")
                ? customerReturnsApi.GetAsync(customerId: _customerId, page: 1, pageSize: 30)
                : Task.FromResult(new List<CustomerReturnListDto>());
            var refundsTask = permissions.Has("customers.view")
                ? customerRefundsApi.GetAsync(customerId: _customerId, page: 1, pageSize: 30)
                : Task.FromResult(new List<CustomerRefundListDto>());
            var currenciesTask = ratesApi.GetCurrenciesAsync(onlyEnabled: true);

            await Task.WhenAll(customerTask, ledgerTask, salesTask, casesTask, paymentsTask, returnsTask, refundsTask, currenciesTask);

            Customer = await customerTask;
            Initials = string.Concat(Customer.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Take(2).Select(x => char.ToUpperInvariant(x[0])));

            Replace(Debts, Customer.DebtBalances.Where(x => x.Amount > 0).Select(x => new CurrencyBalanceRow(x, false)));
            Replace(Credits, Customer.CreditBalances.Where(x => x.Amount > 0).Select(x => new CurrencyBalanceRow(x, true)));
            Replace(Ledger, (await ledgerTask).Content ?? []);
            Replace(Sales, ((await salesTask).Content ?? []).Select(x => new CustomerSaleRow(x)));
            Replace(Cases, await casesTask);
            Replace(Payments, await paymentsTask);
            Replace(Returns, await returnsTask);
            Replace(Refunds, await refundsTask);
            Replace(Currencies, (await currenciesTask).Where(x => x.IsEnabled));
            SelectedCurrency ??= Currencies.FirstOrDefault(x => x.IsBase) ?? Currencies.FirstOrDefault();
            CanRefund = permissions.Has("customers.refund") && Customer.CreditBalances.Any(x => x.Amount > 0);
            IsLoaded = true;
        }
        catch (Exception ex)
        {
            Error = Describe(ex);
        }
        finally
        {
            IsLoading = false;
            NotifyAll();
        }
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
            target.Add(item);
    }

    private void NotifyAll()
    {
        OnPropertyChanged(nameof(HasPhone));
        OnPropertyChanged(nameof(HasEmail));
        OnPropertyChanged(nameof(HasAddress));
        OnPropertyChanged(nameof(HasDebts));
        OnPropertyChanged(nameof(HasCredits));
        OnPropertyChanged(nameof(HasCases));
        OnPropertyChanged(nameof(HasLedger));
        OnPropertyChanged(nameof(HasSales));
        OnPropertyChanged(nameof(HasPayments));
        OnPropertyChanged(nameof(HasReturns));
        OnPropertyChanged(nameof(HasRefunds));
        OnPropertyChanged(nameof(HasError));
    }

    private static string Describe(Exception exception) => exception is ApiException api
        ? ApiErrors.Describe(api)
        : Loc.Instance["err_no_connection"];
}

public sealed record CurrencyBalanceRow(CurrencyAmountDto Balance, bool IsCredit)
{
    public string Amount => $"{Balance.Amount:N0} {Balance.Currency}";
}

public sealed record CustomerSaleRow(SaleDto Sale)
{
    public string Date => Sale.SaleDate.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
    public string Total => $"{Sale.TotalAmount:N0} UZS";
    public string Summary => string.Join(", ", Sale.Items.Take(2).Select(x => x.ProductName))
        + (Sale.Items.Count > 2 ? $" +{Sale.Items.Count - 2}" : "");
}
