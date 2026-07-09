using System.Collections.ObjectModel;
using Cartex.Mobile.Agent.Data;
using Cartex.Mobile.Agent.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.Mobile.Agent.ViewModels;

public partial class CustomersViewModel(AgentDb db, SyncService sync) : ObservableObject
{
    public ObservableCollection<CustomerRow> Customers { get; } = [];

    [ObservableProperty] private string _search = "";
    [ObservableProperty] private bool _isRefreshing;

    private string _currency = "";

    partial void OnSearchChanged(string value) => _ = LoadAsync();

    public async Task AppearAsync()
    {
        _currency = await db.GetMetaAsync("base_currency") ?? "";
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var items = await db.SearchCustomersAsync(Search);
        Customers.Clear();
        foreach (var c in items)
            Customers.Add(new CustomerRow(c, _currency));
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await sync.SyncAsync();
        await LoadAsync();
        IsRefreshing = false;
    }

    [RelayCommand]
    private Task OpenAsync(CustomerRow row) =>
        Shell.Current.GoToAsync("customer", new Dictionary<string, object> { ["customerId"] = row.Customer.Id });
}

public sealed record CustomerRow(LocalCustomer Customer, string Currency)
{
    public string DebtText => Customer.DebtBalance > 0 ? $"{Customer.DebtBalance:N0} {Currency}" : Loc.Instance["no_debt"];
    public bool HasDebt => Customer.DebtBalance > 0;
}
