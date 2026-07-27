using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Querying;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;
using Cartex.Shared.Models.Ordering;
using Cartex.Shared.Models.Sales;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Refit;

namespace Cartex.Mobile.Store.ViewModels;

public partial class TradeViewModel(
    IOrderingApi orderingApi,
    ISalesApi salesApi,
    MobilePermissions permissions,
    WarehouseContext warehouse,
    OrderingHubService orderingHub) : ObservableObject
{
    public ObservableCollection<TradeQueueRow> Carts { get; } = [];
    public ObservableCollection<TradeSaleRow> Sales { get; } = [];
    public ObservableCollection<QueueStatusChip> QueueStatuses { get; } =
    [
        new("Open", Loc.Instance["queue_open"]),
        new("Confirmed", Loc.Instance["queue_confirmed"]),
        new("CheckedOut", Loc.Instance["queue_sold"]),
        new("Cancelled", Loc.Instance["queue_cancelled"])
    ];

    [ObservableProperty] private string _section = "queue";
    [ObservableProperty] private string _selectedStatus = "Open";
    [ObservableProperty] private bool _hasQueueAccess = true;
    [ObservableProperty] private bool _hasSalesAccess = true;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private int _todayCount;
    [ObservableProperty] private string _todayTotal = "0";
    private bool _hubWired;

    public bool IsQueue => Section == "queue";
    public bool IsSales => Section == "sales";
    public bool IsQueueEmpty => Carts.Count == 0;
    public bool IsSalesEmpty => Sales.Count == 0;

    public async Task AppearAsync()
    {
        if (!_hubWired)
        {
            _hubWired = true;
            orderingHub.CartsChanged += OnCartsChanged;
            orderingHub.Resynced += OnHubResynced;
        }
        await orderingHub.EnsureStartedAsync();
        HasQueueAccess = permissions.HasAny("sales.pick", "sales.create", "sales.view", "sales.viewAll");
        HasSalesAccess = permissions.HasAny("sales.view", "sales.viewAll");
        if (!HasQueueAccess && HasSalesAccess)
            Section = "sales";
        SetSelectedStatus(QueueStatuses.First(x => x.Status == SelectedStatus));
        await LoadAsync();
    }

    private void OnCartsChanged(string kind)
    {
        if (kind == "Queue" && IsQueue)
            _ = LoadQueueAsync();
    }

    private void OnHubResynced()
    {
        if (IsQueue)
            _ = LoadQueueAsync();
    }

    partial void OnSectionChanged(string value)
    {
        OnPropertyChanged(nameof(IsQueue));
        OnPropertyChanged(nameof(IsSales));
        _ = LoadAsync();
    }

    [RelayCommand]
    private void ShowQueue() => Section = "queue";

    [RelayCommand]
    private void ShowSales() => Section = "sales";

    [RelayCommand]
    private async Task SelectQueueStatusAsync(QueueStatusChip chip)
    {
        if (chip.IsSelected || IsBusy) return;
        SetSelectedStatus(chip);
        await LoadQueueAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadAsync();
        IsRefreshing = false;
    }

    [RelayCommand]
    private async Task OpenQueueAsync(TradeQueueRow row)
    {
        await Shell.Current.GoToAsync($"handoff?code={row.Cart.AggregateCode}");
    }

    [RelayCommand]
    private Task EditQueueAsync(TradeQueueRow row) =>
        row.Cart.Status == "Open" && permissions.Has("sales.create")
            ? Shell.Current.GoToAsync($"checkout?code={row.Cart.AggregateCode}")
            : Task.CompletedTask;

    [RelayCommand]
    private async Task CancelQueueAsync(TradeQueueRow row)
    {
        var page = Shell.Current.CurrentPage;
        if (!await page.DisplayAlertAsync(Loc.Instance["cart_cancel_title"], Loc.Instance["cart_cancel_confirm"], Loc.Instance["yes"], Loc.Instance["no"]))
            return;

        try
        {
            await orderingApi.UpdateStatusAsync(row.Cart.AggregateCode, new UpdateCartStatusRequest("Cancelled"));
            Ui.Toast(Loc.Instance["cart_cancelled"]);
            await LoadQueueAsync();
        }
        catch (Exception ex)
        {
            Ui.Toast(ex is ApiException api ? ApiErrors.Describe(api) : Loc.Instance["err_no_connection"]);
        }
    }

    private async Task LoadAsync()
    {
        if (IsBusy) return;
        if (IsQueue && !HasQueueAccess || IsSales && !HasSalesAccess) return;

        IsBusy = true;
        Error = null;
        try
        {
            if (IsQueue)
                await LoadQueueCoreAsync();
            else
                await LoadSalesCoreAsync();
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

    private async Task LoadQueueAsync()
    {
        if (!IsQueue) return;
        await LoadAsync();
    }

    private async Task LoadQueueCoreAsync()
    {
        if (!await warehouse.EnsureSelectedAsync())
        {
            Carts.Clear();
            OnPropertyChanged(nameof(IsQueueEmpty));
            return;
        }

        var items = await orderingApi.GetAllAsync(SelectedStatus, warehouse.WarehouseId);
        Carts.Clear();
        foreach (var cart in items)
            Carts.Add(new TradeQueueRow(cart));
        OnPropertyChanged(nameof(IsQueueEmpty));
    }

    private async Task LoadSalesCoreAsync()
    {
        var totalsTask = salesApi.GetTotalsAsync(fromDate: DateTime.Today, toDate: DateTime.Today.AddDays(1));
        var listTask = salesApi.QueryAsync(QueryRequest.Create().Page(1, 30).Sort("CreatedAt", true).Build());
        var totals = await totalsTask;
        var list = (await listTask).Content ?? [];
        TodayCount = totals.Count;
        TodayTotal = totals.TotalAmount.ToString("N0");
        Sales.Clear();
        foreach (var sale in list)
            Sales.Add(new TradeSaleRow(sale));
        OnPropertyChanged(nameof(IsSalesEmpty));
    }

    private void SetSelectedStatus(QueueStatusChip selected)
    {
        SelectedStatus = selected.Status;
        foreach (var chip in QueueStatuses)
            chip.IsSelected = ReferenceEquals(chip, selected);
    }
}

public sealed partial class QueueStatusChip(string status, string text) : ObservableObject
{
    public string Status { get; } = status;
    public string Text { get; } = text;

    [ObservableProperty] private bool _isSelected;
}

public sealed record TradeQueueRow(CartListDto Cart)
{
    public string CreatedBy => string.IsNullOrEmpty(Cart.CreatedByName) ? "—" : Cart.CreatedByName;
    public string TimeText => Cart.CreatedAt.ToLocalTime().ToString("HH:mm");
    public string CustomerText => string.IsNullOrEmpty(Cart.CustomerName) ? Loc.Instance["no_customer"] : Cart.CustomerName;
    public string SummaryText => $"{Cart.ItemCount} {Loc.Instance["items_short"]} • {Cart.EstimatedTotal:N0} UZS";
    public string NoteText => Cart.Note ?? "";
    public bool HasNote => !string.IsNullOrEmpty(Cart.Note);
    public bool CanCancel => Cart.Status == "Open";
    public bool CanEdit => Cart.Status == "Open";
}

public sealed record TradeSaleRow(SaleDto Sale)
{
    private DateTime Local => Sale.SaleDate.Kind == DateTimeKind.Utc ? Sale.SaleDate.ToLocalTime() : Sale.SaleDate;
    public string Total => Sale.TotalAmount.ToString("N0") + " UZS";
    public string SubLine => Local.ToString("dd.MM HH:mm") + (string.IsNullOrEmpty(Sale.CustomerName) ? "" : "  •  " + Sale.CustomerName);
    public bool HasCash => Sale.PaidCash > 0;
    public bool HasCard => Sale.PaidCard > 0;
}
