using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Customers;
using Cartex.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Irihi.Avalonia.Shared.Contracts;

namespace Cartex.UI.ViewModels;

/// One row the user can tick. The statement already knows every document a customer has, so the
/// act is picked from there rather than from four separate lists.
public partial class ActDocumentRow(
    string kind, long id, string number, DateTime occurredAt, string summary, decimal amount)
    : ObservableObject
{
    [ObservableProperty] private bool _isSelected;

    public string Kind { get; } = kind;
    public long Id { get; } = id;
    public string Number { get; } = number;
    public DateTime OccurredAt { get; } = occurredAt;
    public string Summary { get; } = summary;

    /// Hujjatning mijoz balansiga ta'siri: qarz qo'shsa musbat, yopsa manfiy. To'liq
    /// to'langan savdo balansga tegmaydi — shuning uchun nol chiziqcha bo'lib ko'rsatiladi,
    /// hujjatning o'z summasi esa izohda turadi.
    public decimal Amount { get; } = amount;

    public string AmountText => Amount == 0 ? "—" : $"{Amount:+#,##0;-#,##0}";

    public string DateText => OccurredAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
    public string KindLabel => LocalizationManager.Instance[$"act_kind_{Kind}"];
}

public partial class ConsolidatedActViewModel : ViewModelBase, IDialogContext
{
    private readonly ICustomersApi _api;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private readonly IPrinterService _printer;
    private readonly IBusinessApi _businessApi;
    private readonly long _customerId;

    public ConsolidatedActViewModel(
        ICustomersApi api, IToastService toast, IBusyService busy, IPrinterService printer,
        IBusinessApi businessApi, long customerId, string customerName,
        IEnumerable<CustomerStatementEntryDto> timeline)
    {
        _api = api;
        _toast = toast;
        _busy = busy;
        _printer = printer;
        _businessApi = businessApi;
        _customerId = customerId;
        CustomerName = customerName;

        foreach (var entry in timeline)
        {
            if (Map(entry.Type) is not { } kind || entry.DocumentId is not { } id) continue;
            var row = new ActDocumentRow(kind, id, entry.DocumentNumber, entry.OccurredAt, entry.Summary,
                entry.Debit - entry.Credit);
            row.PropertyChanged += (_, _) => NotifySelection();
            Documents.Add(row);
        }
    }

    public string CustomerName { get; }
    public ObservableCollection<ActDocumentRow> Documents { get; } = [];
    public ObservableCollection<ConsolidatedActLineDto> Lines { get; } = [];

    [ObservableProperty] private ConsolidatedActDto? _act;

    public bool HasSelection => Documents.Any(x => x.IsSelected);
    public bool HasAct => Act is not null;
    public bool HasDocuments => Documents.Count > 0;
    public int SelectedCount => Documents.Count(x => x.IsSelected);
    public bool AllSelected => HasDocuments && Documents.All(x => x.IsSelected);

    /// Tanlanganlarning balansga sof ta'siri — "Tuzish" bosilmasdan oldin ham ko'rinadi,
    /// shunda nima tuzilayotgani oldindan ma'lum bo'ladi.
    public decimal SelectedAmount => Documents.Where(x => x.IsSelected).Sum(x => x.Amount);

    private void NotifySelection()
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedAmount));
        OnPropertyChanged(nameof(AllSelected));
    }

    [RelayCommand]
    private void ToggleAll()
    {
        var select = !AllSelected;
        foreach (var row in Documents) row.IsSelected = select;
        NotifySelection();
    }

    partial void OnActChanged(ConsolidatedActDto? value)
    {
        Lines.Clear();
        foreach (var line in value?.Lines ?? []) Lines.Add(line);
        OnPropertyChanged(nameof(HasAct));
    }

    [RelayCommand]
    private async Task GenerateAsync()
    {
        var selected = Documents.Where(x => x.IsSelected)
            .Select(x => new ConsolidatedActSelection(x.Kind, x.Id)).ToList();
        if (selected.Count == 0) { _toast.Warning(L["no_items"]); return; }
        try
        {
            using (_busy.Begin(L["loading"]))
                Act = await _api.GetConsolidatedActAsync(_customerId, new ConsolidatedActRequest(_customerId, selected));
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task PrintAsync()
    {
        if (Act is not { } act) return;
        try
        {
            Cartex.Shared.Models.Business.BusinessDto? business = null;
            try { business = await _businessApi.GetAsync(); } catch { }

            var document = new PreviewDocument(
                DateTime.Now, null, act.CustomerName,
                [.. act.Lines.Select(x => new PreviewLine(
                    x.ProductName, x.NetQuantity, x.UnitName,
                    x.NetQuantity > 0 ? Math.Round(x.NetAmount / x.NetQuantity, 2) : 0, x.NetAmount))],
                0, act.ConsumedAmount, null);

            var options = new ProformaPrintOptions(null, ActFooter(act), 32, "A4");
            var target = _printer.ProformaTarget(options);
            if (string.IsNullOrWhiteSpace(target.Printer))
            {
                _toast.Error(L["printer_not_set"]);
                return;
            }
            var paper = target.Paper is "a5" ? "a5" : "a4";

            var title = $"{L["consolidated_act"]} · {act.FromDate:dd.MM.yyyy} — {act.ToDate:dd.MM.yyyy}";
            var pages = ProformaDocumentRenderer.Render(
                document, title, business, options,
                paper, _printer.GetPrinterCapabilities(target.Printer).SupportsColor,
                L["consolidated_act"].ToUpperInvariant());

            using (_busy.Begin(L["loading"]))
                WindowsImagePrinter.Print(target.Printer!, pages, paper, paper, "portrait", 1, 1, null);
            _toast.Success(L["success"]);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    /// The money summary rides in the footer so the act reconciles on the same page as the goods.
    private string ActFooter(ConsolidatedActDto act) =>
        $"{L["consumed_goods"]}: {act.ConsumedAmount:N0}    " +
        $"{L["settled"]}: {act.PaidAmount:N0}    " +
        $"{L["remaining_debt"]}: {act.RemainingDebt:N0}";

    public event EventHandler<object?>? RequestClose;

    [RelayCommand]
    private void Close() => RequestClose?.Invoke(this, null);

    void IDialogContext.Close() => RequestClose?.Invoke(this, null);

    private static string? Map(string type) => type switch
    {
        "Sale" => "sale",
        "CustomerReturn" => "return",
        "CustomerPayment" => "payment",
        "CustomerRefund" => "refund",
        _ => null
    };
}
