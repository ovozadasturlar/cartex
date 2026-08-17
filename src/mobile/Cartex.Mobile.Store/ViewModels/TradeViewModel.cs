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
    MobilePrintDispatcher printDispatcher,
    SalesPolicyCache policy) : ObservableObject, IQueryAttributable
{
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("section", out var value) && value?.ToString() is { Length: > 0 } section && section != Section)
            Section = section;
    }

    public RangeObservableCollection<TradeQueueRow> Carts { get; } = [];
    public RangeObservableCollection<TradeSaleRow> Sales { get; } = [];
    public RangeObservableCollection<TradeShiftRow> Shifts { get; } = [];
    public ObservableCollection<QueueStatusChip> QueueStatuses { get; } =
    [
        new("Open", Loc.Instance["queue_open"]) { IsSelected = true },
        new("Confirmed", Loc.Instance["queue_confirmed"]),
        new("CheckedOut", Loc.Instance["queue_sold"]),
        new("Cancelled", Loc.Instance["queue_cancelled"])
    ];
    public ObservableCollection<SalesPeriodChip> SalesPeriods { get; } =
    [
        new("today", Loc.Instance["filter_today"]) { IsSelected = true },
        new("week", Loc.Instance["filter_week"]),
        new("month", Loc.Instance["filter_month"]),
        new("all", Loc.Instance["filter_all"])
    ];

    [ObservableProperty] private string _section = "queue";
    [ObservableProperty] private string _selectedStatus = "Open";
    [ObservableProperty] private string _salesPeriod = "today";
    [ObservableProperty] private string _salesSearch = "";
    [ObservableProperty] private bool _hasQueueAccess = true;
    [ObservableProperty] private bool _hasSalesAccess = true;
    [ObservableProperty] private bool _hasZReportAccess;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private bool _isLoadingMoreSales;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private int _todayCount;
    [ObservableProperty] private string _todayTotal = "0";

    private DateTime _lastLoadedAt;
    private int _salesPage = 1;
    private bool _hasMoreSales = true;

    public bool IsQueue => Section == "queue";
    public bool IsSales => Section == "sales";
    public bool IsZReports => Section == "zreports";
    public bool IsQueueEmpty => Carts.Count == 0;
    public bool IsSalesEmpty => Sales.Count == 0;
    public bool IsZReportsEmpty => Shifts.Count == 0;

    public async Task AppearAsync()
    {
        orderingHub.CartsChanged -= OnCartsChanged;
        orderingHub.CartsChanged += OnCartsChanged;
        orderingHub.Resynced -= OnHubResynced;
        orderingHub.Resynced += OnHubResynced;
        _ = orderingHub.EnsureStartedAsync();
        HasQueueAccess = permissions.HasAny("sales.pick", "sales.create", "sales.view", "sales.viewAll")
            && policy.Current.AllowSaleQueue;
        HasSalesAccess = permissions.HasAny("sales.view", "sales.viewAll");
        HasZReportAccess = permissions.HasAny("shifts.view", "shifts.viewAll") && printDispatcher.CanPrintZReport;
        if (!HasQueueAccess && HasSalesAccess)
            Section = "sales";
        else if (!HasQueueAccess && HasZReportAccess)
            Section = "zreports";
        SetSelectedStatus(QueueStatuses.First(x => x.Status == SelectedStatus));
        if (DateTime.UtcNow - _lastLoadedAt < TimeSpan.FromSeconds(45)) return;
        await LoadAsync();
    }

    public void Disappear()
    {
        orderingHub.CartsChanged -= OnCartsChanged;
        orderingHub.Resynced -= OnHubResynced;
        _lastLoadedAt = DateTime.MinValue;
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
    private async Task SearchSalesAsync()
    {
        if (IsBusy) return;
        await LoadSalesCoreAsync(resetPaging: true);
    }

    [RelayCommand]
    private async Task ClearSalesSearchAsync()
    {
        SalesSearch = "";
        await LoadSalesCoreAsync(resetPaging: true);
    }

    [RelayCommand]
    private async Task SelectSalesPeriodAsync(SalesPeriodChip chip)
    {
        if (chip.IsSelected || IsBusy) return;
        SetSelectedPeriod(chip);
        await LoadSalesCoreAsync(resetPaging: true);
    }

    [RelayCommand]
    private async Task LoadMoreSalesAsync()
    {
        if (IsBusy || IsLoadingMoreSales || !_hasMoreSales || !IsSales) return;
        IsLoadingMoreSales = true;
        try
        {
            _salesPage++;
            await LoadSalesCoreAsync(resetPaging: false);
        }
        catch (Exception ex)
        {
            Ui.Toast(ex is ApiException api ? ApiErrors.Describe(api) : Loc.Instance["err_no_connection"]);
        }
        finally
        {
            IsLoadingMoreSales = false;
        }
    }

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
    private Task OpenSaleAsync(TradeSaleRow row) =>
        Shell.Current.GoToAsync($"sale/detail?id={row.Sale.Id}");

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

    private bool _loading;

    private async Task LoadAsync()
    {
        if (_loading) return;
        if (IsQueue && !HasQueueAccess || IsSales && !HasSalesAccess || IsZReports && !HasZReportAccess) return;

        _loading = true;
        IsBusy = IsQueue ? Carts.Count == 0 : IsSales ? Sales.Count == 0 : Shifts.Count == 0;
        Error = null;
        try
        {
            if (IsQueue)
                await LoadQueueCoreAsync();
            else if (IsSales)
                await LoadSalesCoreAsync(resetPaging: true);
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
            _loading = false;
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

        var status = SelectedStatus;
        var items = await Task.Run(() => orderingApi.GetAllAsync(status, warehouse.WarehouseId, "Queue"));
        Carts.ReplaceAll(items.Select(cart => new TradeQueueRow(cart)));
        OnPropertyChanged(nameof(IsQueueEmpty));
    }

    private async Task LoadSalesCoreAsync(bool resetPaging = true)
    {
        if (resetPaging)
        {
            _salesPage = 1;
            _hasMoreSales = true;
        }

        DateTime? fromDate = null;
        DateTime? toDate = null;
        if (SalesPeriod == "today")
        {
            fromDate = DateTime.Today;
            toDate = DateTime.Today.AddDays(1);
        }
        else if (SalesPeriod == "week")
        {
            fromDate = DateTime.Today.AddDays(-7);
            toDate = DateTime.Today.AddDays(1);
        }
        else if (SalesPeriod == "month")
        {
            fromDate = DateTime.Today.AddMonths(-1);
            toDate = DateTime.Today.AddDays(1);
        }

        var queryBuilder = QueryRequest.Create()
            .Page(_salesPage, 30)
            .Sort("CreatedAt", true);

        if (!string.IsNullOrWhiteSpace(SalesSearch))
            queryBuilder = queryBuilder.Search(SalesSearch.Trim());
        if (fromDate.HasValue)
            queryBuilder = queryBuilder.Filter("FromDate", fromDate.Value.ToString("o"));
        if (toDate.HasValue)
            queryBuilder = queryBuilder.Filter("ToDate", toDate.Value.ToString("o"));
        if (warehouse.WarehouseId is { } salesWarehouseId)
            queryBuilder = queryBuilder.Filter("WarehouseId", salesWarehouseId.ToString());

        var query = queryBuilder.Build();
        var search = string.IsNullOrWhiteSpace(SalesSearch) ? null : SalesSearch.Trim();
        var listTask = Task.Run(() => salesApi.QueryListAsync(query));
        var totalsTask = Task.Run(() => salesApi.GetTotalsAsync(
            warehouseId: warehouse.WarehouseId,
            fromDate: fromDate,
            toDate: toDate,
            search: search));

        var listResponse = await listTask;
        var totals = await totalsTask;

        var list = listResponse.Content ?? [];
        TodayCount = totals.Count;
        TodayTotal = Money.Compact(totals.TotalAmount);

        if (resetPaging)
            Sales.ReplaceAll(list.Select(sale => new TradeSaleRow(sale, printDispatcher.CanReprintReceipt, sale.CanResendReceipt)));
        else
            foreach (var sale in list)
                Sales.Add(new TradeSaleRow(sale, printDispatcher.CanReprintReceipt, sale.CanResendReceipt));

        _hasMoreSales = list.Count >= 30;
        OnPropertyChanged(nameof(IsSalesEmpty));
    }

    private async Task LoadZReportsCoreAsync()
    {
        var response = await Task.Run(() => shiftsApi.GetHistoryAsync(1, 30));
        Shifts.ReplaceAll((response.Content?.Where(x => !x.IsOpen) ?? []).Select(shift => new TradeShiftRow(shift)));
        OnPropertyChanged(nameof(IsZReportsEmpty));
    }

    private void SetSelectedStatus(QueueStatusChip selected)
    {
        SelectedStatus = selected.Status;
        foreach (var chip in QueueStatuses)
            chip.IsSelected = ReferenceEquals(chip, selected);
    }

    private void SetSelectedPeriod(SalesPeriodChip selected)
    {
        SalesPeriod = selected.Period;
        foreach (var chip in SalesPeriods)
            chip.IsSelected = ReferenceEquals(chip, selected);
    }
}

public sealed partial class QueueStatusChip(string status, string text) : ObservableObject
{
    public string Status { get; } = status;
    public string Text { get; } = text;

    [ObservableProperty] private bool _isSelected;
}

public sealed partial class SalesPeriodChip(string period, string text) : ObservableObject
{
    public string Period { get; } = period;
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

public sealed record TradeSaleRow(SaleListDto Sale, bool CanPrint, bool CanResend)
{
    private DateTime Local => Sale.SaleDate.Kind == DateTimeKind.Utc ? Sale.SaleDate.ToLocalTime() : Sale.SaleDate;
    public string Total => Sale.TotalAmount.ToString("N0") + " UZS";
    public string TimeText => Local.ToString("HH:mm");
    public string DateText => Local.ToString("dd.MM.yyyy HH:mm");
    public string CustomerName => string.IsNullOrEmpty(Sale.CustomerName) ? Loc.Instance["no_customer"] : Sale.CustomerName;
    public bool HasCustomer => !string.IsNullOrEmpty(Sale.CustomerName);
    public string UserName => Sale.UserName;
    public string ItemsSummary => Sale.ItemCount switch
    {
        0 => "",
        1 => Sale.FirstItemName ?? "",
        2 => string.IsNullOrEmpty(Sale.SecondItemName) ? (Sale.FirstItemName ?? "") : $"{Sale.FirstItemName}, {Sale.SecondItemName}",
        _ => $"{Sale.FirstItemName}, {Sale.SecondItemName} +{Sale.ItemCount - 2}"
    };
    public bool HasItemsSummary => !string.IsNullOrEmpty(ItemsSummary);
    public string ReceiptToken => Sale.ReceiptToken;
    public bool HasCash => Sale.PaidCash > 0;
    public bool HasCard => Sale.PaidCard > 0;
    public bool HasBonus => Sale.PaidBonus > 0;
    public bool HasAdvance => Sale.PaidAdvance > 0;
    public bool HasDebt => Sale.DebtAmount > 0;
    public string DebtText => Sale.DebtAmount > 0 ? $"{Sale.DebtAmount:N0} UZS" : "";
    public string AdvanceText => Sale.PaidAdvance > 0 ? $"{Sale.PaidAdvance:N0} UZS" : "";
}

public sealed record TradeShiftRow(ShiftHistoryDto Shift)
{
    public string UserName => Shift.UserName;
    public string DateText => Shift.OpenedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
}
