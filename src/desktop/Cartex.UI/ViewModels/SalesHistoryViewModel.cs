using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Querying;
using Cartex.ApiClient.Paging;
using Cartex.Shared.Models.Sales;
using Cartex.UI.Services;
using Cartex.UI.ViewModels.Common;

namespace Cartex.UI.ViewModels;

public partial class SalesHistoryViewModel : ViewModelBase, ILoadable
{
    private readonly ISalesApi _salesApi;
    private readonly IReceiptApi _receiptApi;
    private readonly AuthService _auth;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private readonly IExportService _export;

    public ObservableCollection<SaleDto> Sales { get; } = [];
    public PaginationState Paging { get; } = new();
    public bool CanReturn => _auth.HasPermission("sales.return");
    public bool CanExport => _auth.HasPermission("reports.export");

    [ObservableProperty] private DateTimeOffset _dateFrom = DateTimeOffset.Now.AddDays(-7);
    [ObservableProperty] private DateTimeOffset _dateTo = DateTimeOffset.Now;

    [ObservableProperty] private bool _isReceiptOpen;
    [ObservableProperty] private ReceiptDto? _receipt;
    [ObservableProperty] private SalesTotalsDto? _totals;

    [ObservableProperty] private bool _isReturnOpen;
    [ObservableProperty] private SaleDto? _returningSale;
    public ObservableCollection<ReturnLineItem> ReturnLines { get; } = [];

    public bool IsModalOpen => IsReceiptOpen || IsReturnOpen;
    partial void OnIsReceiptOpenChanged(bool value) => OnPropertyChanged(nameof(IsModalOpen));
    partial void OnIsReturnOpenChanged(bool value) => OnPropertyChanged(nameof(IsModalOpen));

    public bool IsEmpty => Sales.Count == 0;

    public SalesHistoryViewModel(ISalesApi salesApi, IReceiptApi receiptApi, AuthService auth, IToastService toast, IBusyService busy, IExportService export)
    {
        _salesApi = salesApi;
        _receiptApi = receiptApi;
        _auth = auth;
        _toast = toast;
        _busy = busy;
        _export = export;
        Paging.Attach(LoadAsync);
        Paging.ConfigureSort([new(L["date"], "CreatedAt"), new(L["total"], "TotalAmount")], new(L["date"], "CreatedAt"));
        Paging.Descending = true;
    }

    [RelayCommand]
    private async Task Export(string format)
    {
        try
        {
            var from = new DateTimeOffset(DateFrom.Date).UtcDateTime;
            var to = new DateTimeOffset(DateTo.Date.AddDays(1)).UtcDateTime;
            var all = await _salesApi.GetAllAsync(fromDate: from, toDate: to);
            await _export.ExportAsync(L["sale_history"], all,
            [
                new(L["sale_date"], s => s.SaleDate),
                new(L["customer"], s => s.CustomerName),
                new(L["total"], s => s.TotalAmount),
                new(L["cash"], s => s.PaidCash),
                new(L["card"], s => s.PaidCard),
                new(L["bonus"], s => s.PaidBonus),
                new(L["debt"], s => s.DebtAmount),
                new(L["status"], s => s.Status),
                new(L["sold_by"], s => s.UserName),
            ], Enum.Parse<ExportFormat>(format));
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    public async Task LoadAsync()
    {
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                var from = new DateTimeOffset(DateFrom.Date).UtcDateTime;
                var to = new DateTimeOffset(DateTo.Date.AddDays(1)).UtcDateTime;
                var pagedTask = _salesApi.QueryAsync(QueryRequest.Create()
                    .Page(Paging.Page, Paging.PageSize)
                    .Sort(Paging.SortBy, Paging.Descending)
                    .With("fromDate", from)
                    .With("toDate", to)
                    .Build());
                var totalsTask = _salesApi.GetTotalsAsync(null, from, to);
                var paged = (await pagedTask).ToPaged();
                Sales.Clear();
                foreach (var s in paged.Items) Sales.Add(s);
                Paging.Apply(paged.Meta);
                OnPropertyChanged(nameof(IsEmpty));
                Totals = await totalsTask;
            }
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private Task Refresh() { Paging.Page = 1; return LoadAsync(); }

    [RelayCommand]
    private async Task OpenReceiptAsync(SaleDto sale)
    {
        if (sale is null || string.IsNullOrEmpty(sale.ReceiptToken)) return;
        try
        {
            using (_busy.Begin(L["loading"]))
                Receipt = await _receiptApi.GetAsync(sale.ReceiptToken);
            IsReceiptOpen = true;
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private void CloseReceipt() => IsReceiptOpen = false;

    [RelayCommand]
    private void ReturnSale(SaleDto sale)
    {
        if (sale is null || sale.Status == "Returned") return;
        ReturningSale = sale;
        ReturnLines.Clear();
        foreach (var i in sale.Items)
        {
            var remaining = i.Quantity - i.ReturnedQuantity;
            if (remaining > 0)
                ReturnLines.Add(new ReturnLineItem(i.SaleItemId, i.ProductName, remaining));
        }
        IsReturnOpen = true;
    }

    [RelayCommand]
    private void CloseReturn() => IsReturnOpen = false;

    [RelayCommand]
    private async Task ConfirmReturnAsync()
    {
        if (ReturningSale is null) return;
        var lines = ReturnLines
            .Where(l => l.Quantity > 0)
            .Select(l => new ReturnLineRequest(l.SaleItemId, l.Quantity, l.Restock, l.Reason))
            .ToList();
        if (lines.Count == 0) { _toast.Error(L["return_select_qty"]); return; }
        try
        {
            using (_busy.Begin(L["loading"]))
                await _salesApi.ReturnAsync(ReturningSale.Id, new ReturnSaleRequest(ReturningSale.Id, lines));
            _toast.Success(L["success"]);
            IsReturnOpen = false;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _toast.Error(ApiErrors.Describe(ex));
        }
    }
}

public partial class ReturnLineItem(long saleItemId, string productName, decimal remaining) : ObservableObject
{
    public long SaleItemId { get; } = saleItemId;
    public string ProductName { get; } = productName;
    public decimal Remaining { get; } = remaining;

    [ObservableProperty] private decimal _quantity = remaining;
    [ObservableProperty] private bool _restock = true;
    [ObservableProperty] private string? _reason;
}
