using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Querying;
using Cartex.Mobile.Core;
using Cartex.Shared.Models.Sales;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.Mobile.Store.ViewModels;

public partial class SalesViewModel(ISalesApi sales, MobilePermissions perms) : ObservableObject
{
    public ObservableCollection<SaleRow> Sales { get; } = [];

    [ObservableProperty] private bool _hasAccess = true;
    [ObservableProperty] private int _todayCount;
    [ObservableProperty] private string _todayTotal = "0";
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private string? _error;

    public async Task AppearAsync()
    {
        HasAccess = perms.HasAny("sales.view", "sales.viewAll");
        if (!HasAccess) return;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        Error = null;
        try
        {
            var totalsTask = sales.GetTotalsAsync(fromDate: DateTime.Today, toDate: DateTime.Today.AddDays(1));
            var listTask = sales.QueryAsync(QueryRequest.Create().Page(1, 30).Sort("CreatedAt", true).Build());
            var totals = await totalsTask;
            var list = (await listTask).Content ?? [];
            TodayCount = totals.Count;
            TodayTotal = totals.TotalAmount.ToString("N0");
            Sales.Clear();
            foreach (var s in list) Sales.Add(new SaleRow(s));
            IsEmpty = Sales.Count == 0;
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
    private async Task RefreshAsync()
    {
        await LoadAsync();
        IsRefreshing = false;
    }
}

public sealed record SaleRow(SaleDto Sale)
{
    private DateTime Local => Sale.SaleDate.Kind == DateTimeKind.Utc ? Sale.SaleDate.ToLocalTime() : Sale.SaleDate;
    public string Total => Sale.TotalAmount.ToString("N0") + " UZS";
    public string SubLine => Local.ToString("dd.MM HH:mm") + (string.IsNullOrEmpty(Sale.CustomerName) ? "" : "  •  " + Sale.CustomerName);
    public bool HasCash => Sale.PaidCash > 0;
    public bool HasCard => Sale.PaidCard > 0;
}
