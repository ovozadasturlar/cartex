using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Querying;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;
using Cartex.Shared.Models.Ordering;
using Cartex.Shared.Models.Sales;
using Cartex.Shared.Models.Shifts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Refit;

namespace Cartex.Mobile.Store.ViewModels;

public partial class TradeViewModel(
    IOrderingApi orderingApi,
    ISalesApi salesApi,
    IShiftsApi shiftsApi,
    MobilePermissions permissions,
    WarehouseContext warehouse,
    OrderingHubService orderingHub,
    MobilePrintDispatcher printDispatcher) : ObservableObject
{
    public ObservableCollection<TradeQueueRow> Carts { get; } = [];
    public ObservableCollection<TradeSaleRow> Sales { get; } = [];
    public ObservableCollection<TradeShiftRow> Shifts { get; } = [];
    public ObservableCollection<QueueStatusChip> QueueStatuses { get; } =
    [
        new("Open", Loc.Instance["queue_open"]) { IsSelected = true },
        new("Confirmed", Loc.Instance["queue_confirmed"]),
        new("CheckedOut", Loc.Instance["queue_sold"]),
        new("Cancelled", Loc.Instance["queue_cancelled"])
    ];

    [ObservableProperty] private string _section = "queue";
    [ObservableProperty] private string _selectedStatus = "Open";
    [ObservableProperty] private bool _hasQueueAccess = true;
    [ObservableProperty] private bool _hasSalesAccess = true;
    [ObservableProperty] private bool _hasZReportAccess;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private int _todayCount;
    [ObservableProperty] private string _todayTotal = "0";
    private bool _hubWired;
    private DateTime _lastLoadedAt;

    public bool IsQueue => Section == "queue";
    public bool IsSales => Section == "sales";
    public bool IsZReports => Section == "zreports";
    public bool IsQueueEmpty => Carts.Count == 0;
    public bool IsSalesEmpty => Sales.Count == 0;
    public bool IsZReportsEmpty => Shifts.Count == 0;

    public async Task AppearAsync()
    {
        if (!_hubWired)
        {
            _hubWired = true;
            orderingHub.CartsChanged += OnCartsChanged;
            orderingHub.Resynced += OnHubResynced;
        }
        _ = orderingHub.EnsureStartedAsync();
        HasQueueAccess = permissions.HasAny("sales.pick", "sales.create", "sales.view", "sales.viewAll");
        HasSalesAccess = permissions.HasAny("sales.view", "sales.viewAll");
        HasZReportAccess = permissions.HasAny("shifts.view", "shifts.viewAll") && printDispatcher.CanPrintZReport;
        if (!HasQueueAccess && HasSalesAccess)
            Section = "sales";
        SetSelectedStatus(QueueStatuses.First(x => x.Status == SelectedStatus));
        if (DateTime.UtcNow - _lastLoadedAt < TimeSpan.FromSeconds(5)) return;
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
        OnPropertyChanged(nameof(IsZReports));
        _ = LoadAsync();
    }

    [RelayCommand]
    private void ShowQueue() => Section = "queue";

    [RelayCommand]
    private void ShowSales() => Section = "sales";

    [RelayCommand]
    private void ShowZReports() => Section = "zreports";

    [RelayCommand]
    private async Task ReprintSaleAsync(TradeSaleRow row)
    {
        try
        {
            await printDispatcher.ReprintReceiptAsync(row.Sale);
            Ui.Toast(Loc.Instance["print_sent"]);
        }
        catch (Exception ex)
        {
            Ui.Toast(ex is ApiException api ? ApiErrors.Describe(api) : Loc.Instance["err_no_connection"]);
        }
    }

    [RelayCommand]
    private async Task ResendSaleAsync(TradeSaleRow row)
    {
        try
        {
            await salesApi.ResendReceiptAsync(row.Sale.Id);
            Ui.Toast(Loc.Instance["receipt_resent"]);
        }
        catch (Exception ex)
        {
            Ui.Toast(ex is ApiException api ? ApiErrors.Describe(api) : Loc.Instance["err_no_connection"]);
        }
    }

    [RelayCommand]
    private async Task PrintZReportAsync(TradeShiftRow row)
    {
        try
        {
            await printDispatcher.PrintZReportAsync(row.Shift.Id);
            Ui.Toast(Loc.Instance["print_sent"]);
        }
        catch (Exception ex)
        {
            Ui.Toast(ex is ApiException api ? ApiErrors.Describe(api) : Loc.Instance["err_no_connection"]);
        }
    }

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
        row.Cart.Status == "Open" && permissions.HasAny("sales.create", "sales.checkout")
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
        if (IsQueue && !HasQueueAccess || IsSales && !HasSalesAccess || IsZReports && !HasZReportAccess) return;

        IsBusy = true;
        Error = null;
        try
        {
            if (IsQueue)
                await LoadQueueCoreAsync();
            else if (IsSales)
                await LoadSalesCoreAsync();
            else
                await LoadZReportsCoreAsync();
            _lastLoadedAt = DateTime.UtcNow;
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
            Sales.Add(new TradeSaleRow(sale, printDispatcher.CanReprintReceipt, sale.CanResendReceipt));
        OnPropertyChanged(nameof(IsSalesEmpty));
    }

    private async Task LoadZReportsCoreAsync()
    {
        var response = await shiftsApi.GetHistoryAsync(1, 30);
        Shifts.Clear();
        foreach (var shift in response.Content?.Where(x => !x.IsOpen) ?? [])
            Shifts.Add(new TradeShiftRow(shift));
        OnPropertyChanged(nameof(IsZReportsEmpty));
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

public sealed record TradeSaleRow(SaleDto Sale, bool CanPrint, bool CanResend)
{
    private DateTime Local => Sale.SaleDate.Kind == DateTimeKind.Utc ? Sale.SaleDate.ToLocalTime() : Sale.SaleDate;
    public string Total => Sale.TotalAmount.ToString("N0") + " UZS";
    public string SubLine => Local.ToString("dd.MM HH:mm") + (string.IsNullOrEmpty(Sale.CustomerName) ? "" : "  •  " + Sale.CustomerName);
    public bool HasCash => Sale.PaidCash > 0;
    public bool HasCard => Sale.PaidCard > 0;
}

public sealed record TradeShiftRow(ShiftHistoryDto Shift)
{
    public string UserName => Shift.UserName;
    public string DateText => Shift.OpenedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
}
