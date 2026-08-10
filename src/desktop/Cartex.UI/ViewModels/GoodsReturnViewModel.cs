using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Irihi.Avalonia.Shared.Contracts;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.TradeCases;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class GoodsReturnViewModel : ViewModelBase, IDialogContext
{
    private readonly long _caseId;
    private readonly int _caseVersion;
    private readonly ITradeCasesApi _api;
    private readonly IToastService _toast;

    public GoodsReturnViewModel(long caseId, TradeCaseDetailDto detail, ITradeCasesApi api, IToastService toast)
    {
        _caseId = caseId;
        _caseVersion = detail.Version;
        Currency = detail.Currency;
        _api = api;
        _toast = toast;
        foreach (var line in detail.Lines.Where(l => l.CustodyQuantity > 0))
            Lines.Add(new GoodsReturnLineRow(line, RaiseTotals));
        RaiseTotals();
    }

    public string Currency { get; }
    public string[] ConditionOptions => [L["tc_condition_sellable"], L["tc_condition_damaged"]];
    public ObservableCollection<GoodsReturnLineRow> Lines { get; } = [];

    public bool HasLines => Lines.Count > 0;
    public bool HasReturnable => Lines.Any(l => l.Quantity > 0);

    private void RaiseTotals()
    {
        OnPropertyChanged(nameof(HasLines));
        OnPropertyChanged(nameof(HasReturnable));
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        var picked = Lines.Where(l => l.Quantity > 0).ToList();
        if (picked.Count == 0) { _toast.Warning(L["error"]); return; }
        try
        {
            var result = await _api.ReturnAsync(_caseId, new CreateGoodsReturnRequest(
                picked.Select(l => new GoodsReturnLineRequest(
                    l.Line.GoodsIssueLineId,
                    l.Quantity,
                    string.IsNullOrWhiteSpace(l.Reason) ? null : l.Reason.Trim(),
                    l.IsDamaged ? "Damaged" : "Sellable",
                    l.IsDamaged ? "Quarantine" : "SellableRestock")).ToList(),
                IdempotencyKey: Guid.NewGuid().ToString("N"),
                ExpectedCaseVersion: _caseVersion));
            RequestClose?.Invoke(this, result);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    public event EventHandler<object?>? RequestClose;

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(this, null);

    public void Close() => RequestClose?.Invoke(this, null);
}

public partial class GoodsReturnLineRow : ObservableObject
{
    private readonly Action _onChanged;

    public GoodsReturnLineRow(TradeCaseLineDto line, Action onChanged)
    {
        Line = line;
        _onChanged = onChanged;
        _quantity = line.CustodyQuantity;
    }

    public TradeCaseLineDto Line { get; }
    public decimal CustodyQuantity => Line.CustodyQuantity;

    [ObservableProperty] private decimal _quantity;
    [ObservableProperty] private string _reason = "";
    [ObservableProperty] private int _conditionIndex;

    public bool IsDamaged => ConditionIndex == 1;

    partial void OnQuantityChanged(decimal value) => _onChanged();
    partial void OnConditionIndexChanged(int value) => OnPropertyChanged(nameof(IsDamaged));
}
