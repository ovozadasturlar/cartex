using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Querying;
using Cartex.Mobile.Core;
using Cartex.Shared.Models.Common;
using Cartex.Shared.Models.Customers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.Mobile.Store.Services;

namespace Cartex.Mobile.Store.ViewModels;

public partial class CustomersViewModel(
    ICustomersApi customersApi,
    MobilePermissions permissions,
    MobileOfflineService offline,
    MobileAuthService auth) : ObservableObject
{
    private const int PageSize = 30;
    public RangeObservableCollection<StoreCustomerRow> Customers { get; } = [];

    [ObservableProperty] private string _search = "";
    [ObservableProperty] private bool _hasAccess;
    [ObservableProperty] private bool _canCreate;
    [ObservableProperty] private bool _isCreateModalOpen;
    [ObservableProperty] private string _newCustomerName = "";
    [ObservableProperty] private string _newCustomerPhone = "";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private bool _isLoadingMore;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private bool _isOfflinePayOpen;
    [ObservableProperty] private string _offlinePayAmount = "";
    [ObservableProperty] private string _offlinePayMethod = "Cash";
    [ObservableProperty] private string _offlinePayName = "";
    [ObservableProperty] private string _offlinePayDebtText = "";

    public bool HasCustomers => Customers.Count > 0;
    public bool IsOfflinePayCash => OfflinePayMethod == "Cash";
    public bool IsOfflinePayCard => OfflinePayMethod == "Card";

    private CancellationTokenSource? _searchCts;
    private StoreCustomerRow? _offlinePayRow;
    private int _page;
    private bool _hasMore = true;
    private DateTime _lastLoadedAt;

    partial void OnOfflinePayMethodChanged(string value)
    {
        OnPropertyChanged(nameof(IsOfflinePayCash));
        OnPropertyChanged(nameof(IsOfflinePayCard));
    }

    public async Task AppearAsync()
    {
        _ = offline.StartAsync();
        HasAccess = permissions.Has("customers.view");
        CanCreate = permissions.Has("customers.create");
        if (!HasAccess) return;
        if (Customers.Count == 0 || DateTime.UtcNow - _lastLoadedAt > TimeSpan.FromSeconds(20))
            await LoadAsync(reset: true, CancellationToken.None);
    }

    partial void OnSearchChanged(string value)
    {
        _searchCts?.Cancel();
        var owner = _searchCts = new CancellationTokenSource();
        _ = SearchAsync(owner);
    }

    private async Task SearchAsync(CancellationTokenSource owner)
    {
        try
        {
            await Task.Delay(250, owner.Token);
            await LoadAsync(reset: true, owner.Token);
        }
        catch (OperationCanceledException) { }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadAsync(reset: true, CancellationToken.None);
        IsRefreshing = false;
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (!_hasMore || IsBusy || IsRefreshing || IsLoadingMore) return;
        await LoadAsync(reset: false, CancellationToken.None);
    }

    [RelayCommand]
    private Task OpenDetailAsync(StoreCustomerRow row)
    {
        if (offline.ShouldUseOffline)
        {
            if (!permissions.Has("customer_payments.create"))
                Ui.Toast(Loc.Instance["offline_detail_requires_internet"]);
            else if (!offline.PaymentsCapability)
                Ui.Toast(Loc.Instance["offline_capability_off"]);
            else
                OpenOfflinePay(row);
            return Task.CompletedTask;
        }
        return Shell.Current.GoToAsync($"customer/detail?id={row.Customer.Id}");
    }

    private void OpenOfflinePay(StoreCustomerRow row)
    {
        _offlinePayRow = row;
        OfflinePayName = row.Customer.FullName;
        OfflinePayDebtText = row.DebtText;
        OfflinePayAmount = "";
        OfflinePayMethod = "Cash";
        IsOfflinePayOpen = true;
    }

    [RelayCommand]
    private void CloseOfflinePay() => IsOfflinePayOpen = false;

    [RelayCommand]
    private void SelectOfflinePayMethod(string method) => OfflinePayMethod = method;

    [RelayCommand]
    private async Task SaveOfflinePayAsync()
    {
        if (_offlinePayRow is not { } row || IsBusy) return;
        if (!decimal.TryParse(OfflinePayAmount.Trim().Replace(',', '.'),
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out var amount) || amount <= 0)
        {
            Ui.Toast(Loc.Instance["amount_invalid"]);
            return;
        }
        IsBusy = true;
        try
        {
            await offline.EnqueuePaymentAsync(new MobileOfflinePaymentDraft(
                row.Customer.Id, auth.DefaultBranchId, amount, OfflinePayMethod == "Card"));
            var baseCurrency = await offline.BaseCurrencyAsync();
            var debt = row.Customer.DebtBalance - amount;
            var index = Customers.IndexOf(row);
            if (index >= 0)
                Customers[index] = new StoreCustomerRow(row.Customer with
                {
                    DebtBalance = debt,
                    DebtBalances = debt > 0 ? [new CurrencyAmountDto(baseCurrency, debt)] : []
                });
            IsOfflinePayOpen = false;
            Ui.Toast(Loc.Instance["offline_payment_queued"]);
        }
        catch (Exception ex)
        {
            Ui.Toast(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Call(StoreCustomerRow row)
    {
        if (string.IsNullOrWhiteSpace(row.Customer.Phone)) return;
        try { PhoneDialer.Default.Open(row.Customer.Phone!); }
        catch { Ui.Toast(Loc.Instance["phone_action_failed"]); }
    }

    [RelayCommand]
    private void OpenCreateModal()
    {
        if (!CanCreate) return;
        if (offline.ShouldUseOffline)
        {
            Ui.Toast(Loc.Instance["offline_mutation_blocked"]);
            return;
        }
        NewCustomerName = "";
        NewCustomerPhone = "";
        IsCreateModalOpen = true;
    }

    [RelayCommand]
    private void CloseCreateModal() => IsCreateModalOpen = false;

    [RelayCommand]
    private async Task SaveCustomerAsync()
    {
        if (string.IsNullOrWhiteSpace(NewCustomerName) || string.IsNullOrWhiteSpace(NewCustomerPhone))
        {
            Ui.Toast(Loc.Instance["err_fill_all"]);
            return;
        }
        if (offline.ShouldUseOffline)
        {
            Ui.Toast(Loc.Instance["offline_mutation_blocked"]);
            return;
        }
        IsBusy = true;
        try
        {
            var name = NewCustomerName.Trim();
            var id = await customersApi.CreateAsync(new CreateCustomerRequest(name, NewCustomerPhone.Trim(), null, 0));
            IsCreateModalOpen = false;
            await Shell.Current.GoToAsync($"customer/detail?id={id}");
        }
        catch { Ui.Toast(Loc.Instance["err_no_connection"]); }
        finally { IsBusy = false; }
    }

    private async Task LoadAsync(bool reset, CancellationToken cancellationToken)
    {
        if (!HasAccess || IsBusy || (!reset && !_hasMore)) return;
        if (reset) IsBusy = Customers.Count == 0;
        else IsLoadingMore = true;
        Error = null;
        try
        {
            var nextPage = reset ? 1 : _page + 1;
            IReadOnlyList<CustomerDto> rows;
            if (offline.ShouldUseOffline)
                rows = nextPage == 1 ? await offline.SearchCustomersAsync(Search, 200) : [];
            else
            {
                var query = QueryRequest.Create().Page(nextPage, PageSize).Search(Search).Build();
                var response = await Task.Run(() => customersApi.QueryAsync(query));
                rows = response.Content ?? [];
            }
            if (cancellationToken.IsCancellationRequested) return;
            if (reset)
            {
                Customers.ReplaceAll(rows
                    .DistinctBy(x => x.Id)
                    .Select(customer => new StoreCustomerRow(customer)));
            }
            else
            {
                var existing = Customers.Select(x => x.Customer.Id).ToHashSet();
                foreach (var customer in rows.Where(x => existing.Add(x.Id)))
                    Customers.Add(new StoreCustomerRow(customer));
            }
            _page = nextPage;
            _hasMore = rows.Count >= PageSize;
            _lastLoadedAt = DateTime.UtcNow;
            OnPropertyChanged(nameof(HasCustomers));
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested) { }
        catch
        {
            offline.MarkServerUnavailable();
            if (offline.IsEnabled && reset)
            {
                Customers.ReplaceAll((await offline.SearchCustomersAsync(Search, 200))
                    .Select(customer => new StoreCustomerRow(customer)));
                _hasMore = false;
                OnPropertyChanged(nameof(HasCustomers));
            }
            else Error = Loc.Instance["err_no_connection"];
        }
        finally
        {
            IsBusy = false;
            IsLoadingMore = false;
        }
    }
}

public sealed record StoreCustomerRow(CustomerDto Customer)
{
    public string Initials => string.Concat(Customer.FullName
        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Take(2).Select(word => char.ToUpper(word[0])));

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
    public bool HasPhone => !string.IsNullOrWhiteSpace(Customer.Phone);
    public string PhoneText => Customer.Phone ?? "—";
}
