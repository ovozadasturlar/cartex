using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Querying;
using Cartex.Mobile.Core;
using Cartex.Shared.Models.Customers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.Mobile.Store.Services;

namespace Cartex.Mobile.Store.ViewModels;

public partial class CustomersViewModel(
    ICustomersApi customersApi,
    MobilePermissions permissions,
    MobileOfflineService offline) : ObservableObject
{
    private const int PageSize = 30;
    public ObservableCollection<StoreCustomerRow> Customers { get; } = [];

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

    public bool HasCustomers => Customers.Count > 0;

    private CancellationTokenSource? _searchCts;
    private int _page;
    private bool _hasMore = true;
    private DateTime _lastLoadedAt;

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
            Ui.Toast(Loc.Instance["offline_detail_requires_internet"]);
            return Task.CompletedTask;
        }
        return Shell.Current.GoToAsync($"customer/detail?id={row.Customer.Id}");
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
                var response = await customersApi.QueryAsync(
                    QueryRequest.Create().Page(nextPage, PageSize).Search(Search).Build());
                rows = response.Content ?? [];
            }
            if (cancellationToken.IsCancellationRequested) return;
            if (reset) Customers.Clear();
            var existing = Customers.Select(x => x.Customer.Id).ToHashSet();
            foreach (var customer in rows.Where(x => existing.Add(x.Id)))
                Customers.Add(new StoreCustomerRow(customer));
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
                Customers.Clear();
                foreach (var customer in await offline.SearchCustomersAsync(Search, 200))
                    Customers.Add(new StoreCustomerRow(customer));
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
