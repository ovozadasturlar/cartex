using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Querying;
using Cartex.ApiClient.Paging;
using Cartex.Shared.Models.Sales;
using Cartex.UI.Services;
using Cartex.UI.ViewModels.Common;
using Cartex.UI.Views;

namespace Cartex.UI.ViewModels;

public partial class SalesHistoryViewModel : ViewModelBase, ILoadable
{
    private readonly ISalesApi _salesApi;
    private readonly IReceiptApi _receiptApi;
    private readonly AuthService _auth;
    private readonly IDialogService _dialog;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private readonly IExportService _export;
    private readonly IPrinterService _printer;
    private readonly PrintDispatchService _printDispatch;
    private readonly NavigationService _navigation;
    private readonly ReceiptDialogService _receiptDialog;

    public ObservableCollection<SaleDto> Sales { get; } = [];
    public PaginationState Paging { get; } = new();
    public bool CanReturn => _auth.HasPermission("returns.create");
    public bool CanExport => _auth.HasPermission("reports.export");

    [ObservableProperty] private DateTimeOffset _dateFrom = DateTimeOffset.Now.AddDays(-7);
    [ObservableProperty] private DateTimeOffset _dateTo = DateTimeOffset.Now;
    [ObservableProperty] private SalesTotalsDto _totals = new(0, 0, 0, 0);

    public bool IsEmpty => Sales.Count == 0;

    public SalesHistoryViewModel(
        ISalesApi salesApi,
        IReceiptApi receiptApi,
        AuthService auth,
        IDialogService dialog,
        IToastService toast,
        IBusyService busy,
        IExportService export,
        IPrinterService printer,
        PrintDispatchService printDispatch,
        ReceiptDialogService receiptDialog,
        NavigationService navigation)
    {
        _receiptDialog = receiptDialog;
        _salesApi = salesApi;
        _receiptApi = receiptApi;
        _auth = auth;
        _dialog = dialog;
        _toast = toast;
        _busy = busy;
        _export = export;
        _printer = printer;
        _printDispatch = printDispatch;
        _navigation = navigation;
        Paging.Attach(LoadAsync);
        Paging.ConfigureSort([new(L["date"], "CreatedAt"), new(L["total"], "TotalAmount")], new(L["date"], "CreatedAt"));
        Paging.Descending = true;
        _auth.LoggedOut += ResetState;
    }

    private void ResetState()
    {
        Sales.Clear();
    }

    [RelayCommand]
    private async Task Export(string format)
    {
        if (!CanExport) return;
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
        if (sale is null) return;
        var result = await _receiptDialog.ShowAsync(sale);
        if (result is ReceiptDialogResult.Returned or ReceiptDialogResult.CustomerAssigned)
            await LoadAsync();
    }

    [RelayCommand]
    private void ReturnSale(SaleDto sale)
    {
        if (sale is null || sale.Status == "Returned" || !CanReturn) return;
        ServiceLocator.Resolve<ReturnsViewModel>().StartForSale(sale.Id);
        _navigation.RequestMenuNavigation("returns");
    }
}
