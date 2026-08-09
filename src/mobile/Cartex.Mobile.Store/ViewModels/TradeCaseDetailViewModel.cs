using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;
using Cartex.Shared.Models.TradeCases;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Refit;

namespace Cartex.Mobile.Store.ViewModels;

public partial class TradeCaseDetailViewModel(
    ITradeCasesApi tradeCasesApi,
    ISettingsApi settingsApi) : ObservableObject, IQueryAttributable
{
    public ObservableCollection<TradeCaseLineRow> Lines { get; } = [];
    public ObservableCollection<TradeCaseDocumentRow> Documents { get; } = [];
    public ObservableCollection<TradeCaseParticipantDto> Participants { get; } = [];

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isLoaded;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string _pageTitle = "";
    [ObservableProperty] private string _caseNumber = "";
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private string _customerName = "";
    [ObservableProperty] private string _customerPhone = "";
    [ObservableProperty] private string _siteAddress = "";
    [ObservableProperty] private string _location = "";
    [ObservableProperty] private string _workflowText = "";
    [ObservableProperty] private string _issuedText = "";
    [ObservableProperty] private string _returnedText = "";
    [ObservableProperty] private string _custodyText = "";
    [ObservableProperty] private string _outstandingText = "";
    [ObservableProperty] private bool _canIssue;
    [ObservableProperty] private bool _canReturn;
    [ObservableProperty] private bool _canSettle;
    [ObservableProperty] private bool _canReceivePayment;
    [ObservableProperty] private bool _canExport;
    [ObservableProperty] private bool _canClose;
    [ObservableProperty] private bool _canCancel;

    public bool HasPhone => !string.IsNullOrWhiteSpace(CustomerPhone);
    public bool HasSiteAddress => !string.IsNullOrWhiteSpace(SiteAddress);
    public bool HasParticipants => Participants.Count > 0;
    public bool HasLines => Lines.Count > 0;
    public bool HasDocuments => Documents.Count > 0;
    public bool HasError => !string.IsNullOrWhiteSpace(Error);

    private long _caseId;
    private TradeCaseDetailDto? _case;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var value))
            long.TryParse(value.ToString(), out _caseId);
    }

    public Task AppearAsync() => IsLoaded ? ReloadAsync() : LoadAsync();
    public Task ReloadAsync() => LoadAsync();

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    private void Call()
    {
        if (!HasPhone) return;
        try { PhoneDialer.Default.Open(CustomerPhone); }
        catch { Ui.Toast(Loc.Instance["phone_action_failed"]); }
    }

    [RelayCommand]
    private Task IssueAsync() => CanIssue ? Shell.Current.GoToAsync($"case/issue?id={_caseId}") : Task.CompletedTask;

    [RelayCommand]
    private Task ReturnAsync() => CanReturn ? Shell.Current.GoToAsync($"case/return?id={_caseId}") : Task.CompletedTask;

    [RelayCommand]
    private Task SettleAsync() => CanSettle ? Shell.Current.GoToAsync($"case/settle?id={_caseId}") : Task.CompletedTask;

    [RelayCommand]
    private Task ReceivePaymentAsync() => CanReceivePayment ? Shell.Current.GoToAsync($"case/payment?id={_caseId}") : Task.CompletedTask;

    [RelayCommand]
    private Task OpenStatementAsync() => CanExport ? Shell.Current.GoToAsync($"case/statement?id={_caseId}") : Task.CompletedTask;

    [RelayCommand]
    private Task OpenDocumentAsync(TradeCaseDocumentRow row) => row.Document.SaleId is long saleId
        ? Shell.Current.GoToAsync($"sale/detail?id={saleId}")
        : Task.CompletedTask;

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (!CanExport || IsBusy) return;
        var page = Shell.Current.CurrentPage;
        var modeLabel = await page.DisplayActionSheetAsync(Loc.Instance["statement_mode"], Loc.Instance["cancel"], null,
            Loc.Instance["statement_both"], Loc.Instance["statement_timeline"], Loc.Instance["statement_consolidated"]);
        if (string.IsNullOrWhiteSpace(modeLabel) || modeLabel == Loc.Instance["cancel"]) return;
        var mode = modeLabel == Loc.Instance["statement_timeline"] ? "timeline"
            : modeLabel == Loc.Instance["statement_consolidated"] ? "consolidated" : "both";
        var formatLabel = await page.DisplayActionSheetAsync(Loc.Instance["file_format"], Loc.Instance["cancel"], null, "PDF", "XLSX");
        if (formatLabel is not ("PDF" or "XLSX")) return;
        var format = formatLabel.ToLowerInvariant();

        IsBusy = true;
        try
        {
            using var content = await tradeCasesApi.ExportStatementAsync(_caseId, format, mode);
            var bytes = await content.ReadAsByteArrayAsync();
            var safeNumber = string.Concat(CaseNumber.Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_'));
            var path = Path.Combine(FileSystem.CacheDirectory, $"{safeNumber}-{mode}.{format}");
            await File.WriteAllBytesAsync(path, bytes);
            await Share.Default.RequestAsync(new ShareFileRequest(
                Loc.Instance["statement"], new ShareFile(path)));
        }
        catch (Exception ex) { Ui.Toast(Describe(ex)); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CloseCaseAsync()
    {
        if (_case is null || !CanClose || IsBusy) return;
        if (!await Shell.Current.CurrentPage.DisplayAlertAsync(
                Loc.Instance["close_case"], Loc.Instance["close_case_confirm"], Loc.Instance["yes"], Loc.Instance["no"]))
            return;
        await ChangeStatusAsync(() => tradeCasesApi.CloseAsync(_caseId, new ChangeTradeCaseStatusRequest(ExpectedVersion: _case.Version)));
    }

    [RelayCommand]
    private async Task CancelCaseAsync()
    {
        if (_case is null || !CanCancel || IsBusy) return;
        var page = Shell.Current.CurrentPage;
        if (!await page.DisplayAlertAsync(
                Loc.Instance["cancel_case"], Loc.Instance["cancel_case_confirm"], Loc.Instance["yes"], Loc.Instance["no"]))
            return;
        var reason = await page.DisplayPromptAsync(Loc.Instance["cancel_case"], Loc.Instance["cancel_reason_optional"],
            Loc.Instance["apply"], Loc.Instance["later"]);
        await ChangeStatusAsync(() => tradeCasesApi.CancelAsync(_caseId,
            new ChangeTradeCaseStatusRequest(reason, _case.Version)));
    }

    private async Task ChangeStatusAsync(Func<Task> action)
    {
        IsBusy = true;
        try
        {
            await action();
            await LoadAsync();
            Ui.Toast(Loc.Instance["saved_successfully"]);
        }
        catch (Exception ex) { Error = Describe(ex); }
        finally { IsBusy = false; Notify(); }
    }

    private async Task LoadAsync()
    {
        if (_caseId <= 0 || IsLoading) return;
        IsLoading = true;
        Error = null;
        try
        {
            var caseTask = tradeCasesApi.GetByIdAsync(_caseId);
            var settingsTask = settingsApi.GetTradeCaseSettingsAsync();
            var value = _case = await caseTask;
            try
            {
                PageTitle = (await settingsTask).SingularLabel;
            }
            catch
            {
                // Settings are decorative here; the core document must remain available.
                PageTitle = Loc.Instance["cases"];
            }
            CaseNumber = value.CaseNumber;
            Title = value.Title;
            Status = value.Status;
            CustomerName = value.CustomerName;
            CustomerPhone = value.CustomerPhone ?? "";
            SiteAddress = value.SiteAddress ?? "";
            Location = $"{value.BranchName} · {value.WarehouseName}";
            WorkflowText = Loc.Instance[value.Workflow == "CustodyUntilSettlement" ? "workflow_custody" : "workflow_immediate"];

            var issued = value.Lines.Sum(x => x.IssuedQuantity);
            var returned = value.Lines.Sum(x => x.ReturnedQuantity);
            var custody = value.Lines.Sum(x => x.CustodyQuantity);
            IssuedText = issued.ToString("0.###");
            ReturnedText = returned.ToString("0.###");
            CustodyText = custody.ToString("0.###");
            OutstandingText = $"{value.Lines.Sum(x => x.CustodyQuantity * x.UnitPrice):N0} {value.Currency}";

            Replace(Lines, value.Lines.Where(x => x.CustodyQuantity > 0).Select(x => new TradeCaseLineRow(x)));
            Replace(Documents, value.Documents.OrderByDescending(x => x.CreatedAt).Select(x => new TradeCaseDocumentRow(x)));
            Replace(Participants, value.Participants ?? []);

            CanIssue = value.AllowedActions.CanIssue;
            CanReturn = value.AllowedActions.CanReturn;
            CanSettle = value.AllowedActions.CanSettle;
            CanReceivePayment = value.AllowedActions.CanReceivePayment;
            CanExport = value.AllowedActions.CanExportStatement;
            CanClose = value.AllowedActions.CanClose;
            CanCancel = value.AllowedActions.CanCancel;
            IsLoaded = true;
        }
        catch (Exception ex) { Error = Describe(ex); }
        finally { IsLoading = false; Notify(); }
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values) target.Add(value);
    }

    private void Notify()
    {
        OnPropertyChanged(nameof(HasPhone));
        OnPropertyChanged(nameof(HasSiteAddress));
        OnPropertyChanged(nameof(HasParticipants));
        OnPropertyChanged(nameof(HasLines));
        OnPropertyChanged(nameof(HasDocuments));
        OnPropertyChanged(nameof(HasError));
    }

    private static string Describe(Exception exception) => exception is ApiException api
        ? ApiErrors.Describe(api)
        : Loc.Instance["err_no_connection"];
}

public sealed record TradeCaseLineRow(TradeCaseLineDto Line)
{
    public string Quantity => $"{Line.CustodyQuantity:0.###} {Line.UnitName}";
    public string Amount => $"{Line.CustodyQuantity * Line.UnitPrice:N0}";
    public string Source => $"{Line.IssueDocumentNumber} · {Line.IssueDate:dd.MM.yyyy}";
}

public sealed record TradeCaseDocumentRow(TradeCaseDocumentDto Document)
{
    public string TypeText => Loc.Instance[Document.Type switch
    {
        "GoodsIssue" => "document_issue",
        "GoodsReturn" => "document_return",
        "Settlement" => "document_settlement",
        "CustomerPayment" => "document_payment",
        _ => "document"
    }];
    public string Date => $"{Document.BusinessDate:dd.MM.yyyy}";
    public string Amount => Document.Amount > 0 ? $"{Document.Amount:N0}" : $"{Document.Quantity:0.###}";
    public bool CanOpen => Document.SaleId.HasValue;
}
