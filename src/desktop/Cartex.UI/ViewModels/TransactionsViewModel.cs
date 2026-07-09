using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Paging;
using Cartex.Shared.Models.Transactions;
using Cartex.UI.Services;
using Cartex.UI.ViewModels.Common;

namespace Cartex.UI.ViewModels;

public partial class TransactionsViewModel : ViewModelBase, ILoadable
{
    private readonly ITransactionsApi _api;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private readonly IExportService _export;
    private readonly AuthService _auth;

    public ObservableCollection<TransactionDto> Transactions { get; } = [];
    public PaginationState Paging { get; } = new();
    [ObservableProperty] private TransactionsTotalsDto? _totals;

    [ObservableProperty] private DateTimeOffset _dateFrom = DateTimeOffset.Now.AddDays(-30);
    [ObservableProperty] private DateTimeOffset _dateTo = DateTimeOffset.Now;
    [ObservableProperty] private string? _selectedOperationType = "";

    public IReadOnlyList<string> OperationTypeOptions { get; } =
        ["", "Sale", "DebtCharge", "DebtPay", "Cashback", "BonusSpend", "SupplyPay", "CashIn", "CashOut"];

    public bool IsEmpty => Transactions.Count == 0;
    public bool CanExport => _auth.HasPermission("reports.export");

    public TransactionsViewModel(ITransactionsApi api, IToastService toast, IBusyService busy, IExportService export, AuthService auth)
    {
        _api = api;
        _toast = toast;
        _busy = busy;
        _export = export;
        _auth = auth;
        Paging.Attach(LoadAsync);
        Paging.ConfigureSort([new(L["date"], "CreatedAt"), new(L["amount"], "Amount")], new(L["date"], "CreatedAt"));
        Paging.Descending = true;
    }

    [RelayCommand]
    private async Task Export(string format)
    {
        try
        {
            var from = new DateTimeOffset(DateFrom.Date).UtcDateTime;
            var to = new DateTimeOffset(DateTo.Date.AddDays(1)).UtcDateTime;
            var op = string.IsNullOrEmpty(SelectedOperationType) ? null : SelectedOperationType;
            var all = await _api.GetAllAsync(from, to, op);
            await _export.ExportAsync(L["transactions"], all,
            [
                new(L["time"], t => t.CreatedAt),
                new(L["operation_type"], t => t.OperationType),
                new(L["amount"], t => t.Amount),
                new(L["from_account"], t => t.FromAccountName),
                new(L["to_account"], t => t.ToAccountName),
                new(L["user"], t => t.UserName),
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
                var op = string.IsNullOrEmpty(SelectedOperationType) ? null : SelectedOperationType;
                var pagedTask = _api.GetPagedAsync(Paging.Page, Paging.PageSize, Paging.SortBy, Paging.Descending,
                    null, from, to, op);
                var totalsTask = _api.GetTotalsAsync(null, from, to, op);
                var paged = (await pagedTask).ToPaged();
                Transactions.Clear();
                foreach (var t in paged.Items) Transactions.Add(t);
                Paging.Apply(paged.Meta);
                Totals = await totalsTask;
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private Task Refresh() { Paging.Page = 1; return LoadAsync(); }
}
