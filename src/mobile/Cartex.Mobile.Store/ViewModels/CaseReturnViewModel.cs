using System.Collections.ObjectModel;
using System.ComponentModel;
using Cartex.ApiClient.Api;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;
using Cartex.Shared.Models.TradeCases;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Refit;

namespace Cartex.Mobile.Store.ViewModels;

public partial class CaseReturnViewModel(ITradeCasesApi tradeCasesApi) : ObservableObject, IQueryAttributable
{
    public ObservableCollection<CaseReturnLine> Lines { get; } = [];
    public IReadOnlyList<ReturnOption> Conditions { get; } =
    [
        new("Sellable", Loc.Instance["condition_sellable"]),
        new("Opened", Loc.Instance["condition_opened"]),
        new("Damaged", Loc.Instance["condition_damaged"]),
        new("Defective", Loc.Instance["condition_defective"])
    ];
    public IReadOnlyList<ReturnOption> Dispositions { get; } =
    [
        new("SellableRestock", Loc.Instance["disposition_restock"]),
        new("Quarantine", Loc.Instance["disposition_quarantine"]),
        new("Scrap", Loc.Instance["disposition_scrap"]),
        new("SupplierClaim", Loc.Instance["disposition_supplier_claim"])
    ];

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isLoaded;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string _caseTitle = "";
    [ObservableProperty] private string _customerName = "";
    [ObservableProperty] private string _note = "";
    [ObservableProperty] private string _quantityText = "0";

    public bool HasSelection => Lines.Any(x => x.IsSelected);
    public bool HasError => !string.IsNullOrWhiteSpace(Error);

    private long _caseId;
    private TradeCaseDetailDto? _case;
    private readonly string _idempotencyKey = Guid.NewGuid().ToString("N");

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var value)) long.TryParse(value.ToString(), out _caseId);
    }

    public async Task AppearAsync()
    {
        if (IsLoaded || IsLoading || _caseId <= 0) return;
        IsLoading = true;
        try
        {
            _case = await tradeCasesApi.GetByIdAsync(_caseId);
            CaseTitle = $"{_case.CaseNumber} · {_case.Title}";
            CustomerName = _case.CustomerName;
            Lines.Clear();
            foreach (var item in _case.Lines.Where(x => x.CustodyQuantity > 0))
            {
                var row = new CaseReturnLine(item, Conditions[0], Dispositions[0]);
                row.PropertyChanged += OnLineChanged;
                Lines.Add(row);
            }
            IsLoaded = true;
            Recalculate();
        }
        catch (Exception ex) { Error = Describe(ex); }
        finally { IsLoading = false; Notify(); }
    }

    [RelayCommand]
    private void Increment(CaseReturnLine line)
    {
        line.IsSelected = true;
        line.Quantity = Math.Min(line.Source.CustodyQuantity, line.Quantity + line.IncrementStep);
    }

    [RelayCommand]
    private void Decrement(CaseReturnLine line)
    {
        var next = Math.Max(line.QuantityStep, line.Quantity - line.IncrementStep);
        if (next < line.Quantity) line.Quantity = next;
    }

    [RelayCommand]
    private void CommitQuantity(CaseReturnLine line)
    {
        if (QuantityInput.TryParse(line.QuantityText, line.QuantityStep, line.Source.AllowsFractional,
                out var quantity, out var error) && quantity <= line.Source.CustodyQuantity)
        {
            line.Quantity = quantity;
            line.IsSelected = true;
            return;
        }
        line.QuantityText = QuantityInput.Format(line.Quantity);
        Ui.Toast(quantity > line.Source.CustodyQuantity
            ? Loc.Instance["return_quantity_exceeded"]
            : Loc.Instance[error switch
            {
                QuantityInputError.FractionNotAllowed => "quantity_integer_required",
                QuantityInputError.StepMismatch => "quantity_step_invalid",
                QuantityInputError.MustBePositive => "quantity_positive_required",
                _ => "quantity_invalid"
            }]);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_case is null || !HasSelection || IsBusy) return;
        var selected = Lines.Where(x => x.IsSelected).ToList();
        foreach (var line in selected) CommitQuantity(line);
        IsBusy = true;
        Error = null;
        try
        {
            var result = await tradeCasesApi.ReturnAsync(_caseId, new CreateGoodsReturnRequest(
                selected.Select(x => new GoodsReturnLineRequest(
                    x.Source.GoodsIssueLineId,
                    x.Quantity,
                    string.IsNullOrWhiteSpace(x.Reason) ? null : x.Reason.Trim(),
                    x.SelectedCondition?.Code ?? "Sellable",
                    x.SelectedDisposition?.Code ?? "SellableRestock")).ToList(),
                Note: string.IsNullOrWhiteSpace(Note) ? null : Note.Trim(),
                IdempotencyKey: _idempotencyKey,
                ExpectedCaseVersion: _case.Version));
            Ui.Toast(string.Format(Loc.Instance["case_return_created_fmt"], result.DocumentNumber));
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex) { Error = Describe(ex); }
        finally { IsBusy = false; Notify(); }
    }

    private void OnLineChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CaseReturnLine.Quantity) or nameof(CaseReturnLine.IsSelected)) Recalculate();
    }

    private void Recalculate()
    {
        QuantityText = Lines.Where(x => x.IsSelected).Sum(x => x.Quantity).ToString("0.###");
        Notify();
    }

    private void Notify()
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(HasError));
    }

    private static string Describe(Exception exception) => exception is ApiException api
        ? ApiErrors.Describe(api)
        : Loc.Instance["err_no_connection"];
}

public partial class CaseReturnLine : ObservableObject
{
    public TradeCaseLineDto Source { get; }
    public string CustodyText => $"{Loc.Instance["with_customer"]}: {Source.CustodyQuantity:0.###} {Source.UnitName}";
    public decimal QuantityStep => QuantityInput.NormalizeStep(Source.QuantityStep, Source.AllowsFractional);
    public decimal IncrementStep => 1m % QuantityStep == 0 ? 1 : QuantityStep;

    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private decimal _quantity;
    [ObservableProperty] private string _quantityText;
    [ObservableProperty] private string _reason = "";
    [ObservableProperty] private ReturnOption? _selectedCondition;
    [ObservableProperty] private ReturnOption? _selectedDisposition;

    public CaseReturnLine(TradeCaseLineDto source, ReturnOption condition, ReturnOption disposition)
    {
        Source = source;
        _quantity = source.CustodyQuantity;
        _quantityText = QuantityInput.Format(_quantity);
        _selectedCondition = condition;
        _selectedDisposition = disposition;
    }

    partial void OnQuantityChanged(decimal value) => QuantityText = QuantityInput.Format(value);
}
