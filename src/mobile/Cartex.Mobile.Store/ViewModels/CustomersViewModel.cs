using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Querying;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;
using Cartex.Shared.Models.Customers;
using Cartex.Shared.Models.Sales;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.Mobile.Store.ViewModels;

public partial class CustomersViewModel(
    ICustomersApi customersApi,
    ISalesApi salesApi,
    IBusinessApi businessApi,
    MobilePermissions permissions) : ObservableObject
{
    public ObservableCollection<StoreCustomerRow> Customers { get; } = [];
    public ObservableCollection<DebtCurrencyOption> DebtCurrencies { get; } = [];
    public ObservableCollection<CustomerLedgerEntryDto> Ledger { get; } = [];
    public ObservableCollection<SaleDto> Sales { get; } = [];

    [ObservableProperty] private string _search = "";
    [ObservableProperty] private bool _hasAccess;
    [ObservableProperty] private bool _canReceivePayment;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private bool _isDetailOpen;
    [ObservableProperty] private bool _isPaymentOpen;
    [ObservableProperty] private bool _isSalesTab;
    [ObservableProperty] private bool _isSalesLoading;
    [ObservableProperty] private StoreCustomerRow? _selectedCustomer;
    [ObservableProperty] private DebtCurrencyOption? _selectedDebtCurrency;
    [ObservableProperty] private string _paymentAmount = "";
    [ObservableProperty] private bool _viaCard;
    [ObservableProperty] private string _baseCurrency = "UZS";
    [ObservableProperty] private string? _error;

    private CancellationTokenSource? _searchCts;

    public bool HasCustomers => Customers.Count > 0;
    public bool HasDebt => DebtCurrencies.Count > 0;
    public bool HasSales => Sales.Count > 0;
    public bool CanUseCard => SelectedDebtCurrency?.Currency == BaseCurrency;

    public async Task AppearAsync()
    {
        HasAccess = permissions.Has("customers.view");
        CanReceivePayment = permissions.Has("customers.receivePayment");
        if (!HasAccess) return;

        try { BaseCurrency = (await businessApi.GetAsync()).Currency; }
        catch { }
        await LoadCustomersAsync(Search, CancellationToken.None);
    }

    partial void OnSearchChanged(string value)
    {
        _searchCts?.Cancel();
        var cts = _searchCts = new CancellationTokenSource();
        _ = SearchAsync(value, cts);
    }

    partial void OnSelectedDebtCurrencyChanged(DebtCurrencyOption? value)
    {
        if (value is null) return;
        if (ViaCard && !CanUseCard) ViaCard = false;
        OnPropertyChanged(nameof(CanUseCard));
    }

    partial void OnBaseCurrencyChanged(string value) => OnPropertyChanged(nameof(CanUseCard));

    private async Task SearchAsync(string value, CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(250, cts.Token);
            await LoadCustomersAsync(value, cts.Token);
        }
        catch (OperationCanceledException) { }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadCustomersAsync(Search, CancellationToken.None);
        IsRefreshing = false;
    }

    [RelayCommand]
    private async Task OpenDetailAsync(StoreCustomerRow row)
    {
        if (IsBusy) return;
        IsBusy = true;
        Error = null;
        try
        {
            await LoadDetailAsync(row.Customer.Id);
            IsDetailOpen = true;
        }
        catch
        {
            Error = Loc.Instance["err_no_connection"];
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void CloseDetail()
    {
        IsPaymentOpen = false;
        IsDetailOpen = false;
        SelectedCustomer = null;
        Ledger.Clear();
        Sales.Clear();
        DebtCurrencies.Clear();
        OnPropertyChanged(nameof(HasDebt));
        OnPropertyChanged(nameof(HasSales));
    }

    [RelayCommand]
    private void SelectProfileTab(string tab) => IsSalesTab = tab == "sales";

    [RelayCommand]
    private void OpenPayment()
    {
        if (!CanReceivePayment || !HasDebt) return;
        PaymentAmount = "";
        ViaCard = false;
        IsPaymentOpen = true;
    }

    [RelayCommand]
    private void ClosePayment() => IsPaymentOpen = false;

    [RelayCommand]
    private void SelectCash() => ViaCard = false;

    [RelayCommand]
    private void SelectCard()
    {
        if (CanUseCard) ViaCard = true;
    }

    [RelayCommand]
    private async Task SavePaymentAsync()
    {
        if (SelectedCustomer is null || SelectedDebtCurrency is null || IsBusy) return;
        if (!decimal.TryParse(PaymentAmount.Replace(" ", ""), out var amount) || amount <= 0)
        {
            Error = Loc.Instance["err_fill_all"];
            return;
        }
        if (amount > SelectedDebtCurrency.Amount)
        {
            Error = Loc.Instance["payment_exceeds_debt"];
            return;
        }

        IsBusy = true;
        Error = null;
        try
        {
            await customersApi.RepayDebtAsync(SelectedCustomer.Customer.Id,
                new RepayDebtRequest(amount, ViaCard, SelectedDebtCurrency.Currency, SelectedDebtCurrency.Currency, Guid.NewGuid().ToString("N")));
            Ui.Toast(Loc.Instance["saved_successfully"]);
            IsPaymentOpen = false;
            await LoadDetailAsync(SelectedCustomer.Customer.Id);
            await LoadCustomersAsync(Search, CancellationToken.None);
        }
        catch (Refit.ApiException ex) { Error = ApiErrors.Describe(ex); }
        catch { Error = Loc.Instance["err_no_connection"]; }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadCustomersAsync(string term, CancellationToken cancellationToken)
    {
        if (!HasAccess) return;
        try
        {
            var response = await customersApi.QueryAsync(QueryRequest.Create().Page(1, 30).Search(term).Build());
            if (cancellationToken.IsCancellationRequested) return;
            Customers.Clear();
            foreach (var customer in response.Content ?? [])
                Customers.Add(new StoreCustomerRow(customer));
            OnPropertyChanged(nameof(HasCustomers));
        }
        catch
        {
            if (!cancellationToken.IsCancellationRequested)
                Error = Loc.Instance["err_no_connection"];
        }
    }

    private async Task LoadDetailAsync(long customerId)
    {
        var detailTask = customersApi.GetByIdAsync(customerId);
        var ledgerTask = customersApi.GetLedgerAsync(customerId, 1, 12);
        var salesTask = salesApi.QueryAsync(QueryRequest.Create().Page(1, 20).Sort("CreatedAt", true).With("customerId", customerId).Build());
        var detail = await detailTask;
        SelectedCustomer = new StoreCustomerRow(detail);
        IsSalesTab = false;
        PopulateDebtCurrencies(detail);
        Ledger.Clear();
        Sales.Clear();
        var ledger = await ledgerTask;
        if (ledger.IsSuccessStatusCode && ledger.Content is not null)
            foreach (var item in ledger.Content)
                Ledger.Add(item);
        try
        {
            IsSalesLoading = true;
            var sales = await salesTask;
            if (sales.IsSuccessStatusCode && sales.Content is not null)
                foreach (var sale in sales.Content)
                    Sales.Add(sale);
        }
        catch { }
        finally { IsSalesLoading = false; }
        OnPropertyChanged(nameof(HasSales));
    }

    private void PopulateDebtCurrencies(CustomerDto customer)
    {
        DebtCurrencies.Clear();
        foreach (var debt in customer.DebtBalances.Where(x => x.Amount > 0))
            DebtCurrencies.Add(new DebtCurrencyOption(debt.Currency, debt.Amount));
        SelectedDebtCurrency = DebtCurrencies.FirstOrDefault(x => x.Currency == BaseCurrency) ?? DebtCurrencies.FirstOrDefault();
        OnPropertyChanged(nameof(HasDebt));
    }
}

public sealed record StoreCustomerRow(CustomerDto Customer)
{
    public string Initials => string.Concat(Customer.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(word => char.ToUpper(word[0])));
    public string DebtText
    {
        get
        {
            var debts = Customer.DebtBalances.Where(x => x.Amount > 0).ToList();
            return debts.Count == 0
                ? Loc.Instance["no_debt"]
                : string.Join(" · ", debts.Select(x => $"{x.Amount:N0} {x.Currency}"));
        }
    }
    public bool HasDebt => Customer.DebtBalances.Any(x => x.Amount > 0);
    public string PhoneText => Customer.Phone ?? "—";
}

public sealed record DebtCurrencyOption(string Currency, decimal Amount)
{
    public string Display => $"{Amount:N0} {Currency}";
}
