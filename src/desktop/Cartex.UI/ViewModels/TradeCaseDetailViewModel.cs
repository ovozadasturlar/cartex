using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.TradeCases;
using Cartex.UI.Services;
using Cartex.UI.Views;

namespace Cartex.UI.ViewModels;

public sealed record TradeCaseLineRow(TradeCaseLineDto Line)
{
    public decimal CustodyAmount => Line.CustodyQuantity * Line.UnitPrice;
}

public partial class TradeCaseDetailViewModel : ViewModelBase, ILoadable
{
    private readonly ITradeCasesApi _api;
    private readonly IStocksApi _stocksApi;
    private readonly NavigationService _navigation;
    private readonly IDialogService _dialog;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private readonly AuthService _auth;
    private readonly PrintDispatchService _print;
    private readonly IFilePickerService _filePicker;
    private long _caseId;

    public TradeCaseDetailViewModel(ITradeCasesApi api, IStocksApi stocksApi, NavigationService navigation,
        IDialogService dialog, IToastService toast, IBusyService busy, AuthService auth,
        PrintDispatchService print, IFilePickerService filePicker)
    {
        _api = api;
        _stocksApi = stocksApi;
        _navigation = navigation;
        _dialog = dialog;
        _toast = toast;
        _busy = busy;
        _auth = auth;
        _print = print;
        _filePicker = filePicker;
    }

    [ObservableProperty] private TradeCaseDetailDto? _detail;

    public ObservableCollection<TradeCaseLineRow> Lines { get; } = [];

    private static readonly TradeCaseDetailDto EmptyDetail = new(
        0, "", DateOnly.MinValue, "", null, 0, "", null, 0, "", 0, "", "", "", "", "",
        null, 0, DateTime.MinValue, DateTime.MinValue, [], [],
        new TradeCaseAllowedActions(false, false, false, false, false, false, false), null);
    public TradeCaseDetailDto DetailDisplay => Detail ?? EmptyDetail;

    public bool HasDetail => Detail is not null;
    public bool HasNote => !string.IsNullOrWhiteSpace(Detail?.Note);
    public bool HasParticipants => Detail?.Participants?.Count > 0;
    public bool HasLines => Lines.Count > 0;
    public bool HasDocuments => Detail?.Documents?.Count > 0;
    public bool CanIssue => Detail is { AllowedActions.CanIssue: true };
    public bool CanReturn => Detail is { AllowedActions.CanReturn: true };
    public bool CanSettle => Detail is { AllowedActions.CanSettle: true };
    public bool CanCancelCase => Detail is { AllowedActions.CanCancel: true };
    public bool CanCloseCase => Detail is { AllowedActions.CanClose: true };
    public bool CanExportStatement => Detail is { AllowedActions.CanExportStatement: true }
        && _auth.HasPermission("statements.export");

    public void Init(long id) => _caseId = id;

    public async Task LoadAsync()
    {
        if (_caseId == 0) return;
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                Detail = await _api.GetByIdAsync(_caseId);
                Lines.Clear();
                foreach (var line in Detail.Lines) Lines.Add(new TradeCaseLineRow(line));
                OnPropertyChanged(nameof(HasLines));
            }
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    partial void OnDetailChanged(TradeCaseDetailDto? value)
    {
        OnPropertyChanged(nameof(DetailDisplay));
        OnPropertyChanged(nameof(HasDetail));
        OnPropertyChanged(nameof(HasNote));
        OnPropertyChanged(nameof(HasParticipants));
        OnPropertyChanged(nameof(HasLines));
        OnPropertyChanged(nameof(HasDocuments));
        OnPropertyChanged(nameof(CanIssue));
        OnPropertyChanged(nameof(CanReturn));
        OnPropertyChanged(nameof(CanSettle));
        OnPropertyChanged(nameof(CanCancelCase));
        OnPropertyChanged(nameof(CanCloseCase));
        OnPropertyChanged(nameof(CanExportStatement));
    }

    [RelayCommand]
    private void Back()
    {
        var list = ServiceLocator.Resolve<TradeCasesViewModel>();
        _navigation.RequestPageNavigation(list);
    }

    [RelayCommand]
    private Task Refresh() => LoadAsync();

    [RelayCommand]
    private async Task OpenIssueAsync()
    {
        if (Detail is null || !CanIssue) return;
        var result = await _dialog.ShowAsync<GoodsIssueDialog, GoodsIssueViewModel, GoodsIssueCreatedDto>(
            new GoodsIssueViewModel(Detail.Id, Detail.WarehouseId, Detail.Currency, Detail.Version, _api, _stocksApi, _toast));
        if (result is null) return;
        _toast.Success(L["tc_issue_done"]);
        if (await _dialog.ConfirmAsync(L["tc_print_now"]))
            try { await _print.PrintIssueNoteAsync(result.Id); }
            catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
        await LoadAsync();
    }

    [RelayCommand]
    private async Task OpenReturnAsync()
    {
        if (Detail is null || !CanReturn) return;
        var result = await _dialog.ShowAsync<GoodsReturnDialog, GoodsReturnViewModel, GoodsReturnCreatedDto>(
            new GoodsReturnViewModel(Detail.Id, Detail, _api, _toast));
        if (result is null) return;
        _toast.Success(L["tc_return_done"]);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task OpenSettleAsync()
    {
        if (Detail is null || !CanSettle) return;
        var result = await _dialog.ShowAsync<TradeCaseSettleDialog, TradeCaseSettleViewModel, TradeCaseSettlementCreatedDto>(
            new TradeCaseSettleViewModel(Detail.Id, Detail, _api, _toast));
        if (result is null) return;
        _toast.Success(L["tc_settle_done"]);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task CancelCaseAsync()
    {
        if (Detail is null || !CanCancelCase) return;
        if (!await _dialog.ConfirmDangerAsync(L["tc_cancel_confirm"])) return;
        try
        {
            using (_busy.Begin(L["loading"]))
                await _api.CancelAsync(Detail.Id, new ChangeTradeCaseStatusRequest(null, Detail.Version));
            _toast.Success(L["success"]);
            await LoadAsync();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task CloseCaseAsync()
    {
        if (Detail is null || !CanCloseCase) return;
        if (!await _dialog.ConfirmAsync(L["tc_close_confirm"])) return;
        try
        {
            using (_busy.Begin(L["loading"]))
                await _api.CloseAsync(Detail.Id, new ChangeTradeCaseStatusRequest(null, Detail.Version));
            _toast.Success(L["success"]);
            await LoadAsync();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task ExportStatementAsync()
    {
        if (Detail is null || !CanExportStatement) return;
        try
        {
            using var content = await _api.ExportStatementAsync(Detail.Id, "pdf", "both");
            var target = await _filePicker.SaveFileAsync($"loyiha-{Detail.CaseNumber}", "pdf");
            if (target is null) return;

            await using (target)
                await content.CopyToAsync(target);
            _toast.Success(L["export_done"]);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task PrintIssueAsync(TradeCaseDocumentDto doc)
    {
        if (doc.Type != "GoodsIssue") return;
        try { await _print.PrintIssueNoteAsync(doc.Id); }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }
}
