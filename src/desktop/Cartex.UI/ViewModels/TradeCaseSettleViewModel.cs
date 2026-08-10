using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Irihi.Avalonia.Shared.Contracts;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.TradeCases;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class TradeCaseSettleViewModel : ViewModelBase, IDialogContext
{
    private readonly long _caseId;
    private readonly int _caseVersion;
    private readonly ITradeCasesApi _api;
    private readonly IToastService _toast;

    public TradeCaseSettleViewModel(long caseId, TradeCaseDetailDto detail, ITradeCasesApi api, IToastService toast)
    {
        _caseId = caseId;
        _caseVersion = detail.Version;
        Currency = detail.Currency;
        _api = api;
        _toast = toast;
        foreach (var line in detail.Lines.Where(l => l.CustodyQuantity > 0))
            Lines.Add(new SettleLineRow(line, RaiseTotals));
    }

    public string Currency { get; }
    public ObservableCollection<SettleLineRow> Lines { get; } = [];

    [ObservableProperty] private decimal _discountAmount;
    [ObservableProperty] private decimal _paidCash;
    [ObservableProperty] private decimal _paidCard;
    [ObservableProperty] private bool _useCustomerAdvance = true;
    [ObservableProperty] private bool _closeWhenEmpty = true;
    [ObservableProperty] private string _note = "";

    public bool HasLines => Lines.Count > 0;
    public decimal SubTotal => Lines.Sum(l => l.LineTotal);
    public decimal DebtAmount => Math.Max(0, SubTotal - DiscountAmount - PaidCash - PaidCard);
    public bool HasDebt => DebtAmount > 0;

    private void RaiseTotals()
    {
        OnPropertyChanged(nameof(HasLines));
        OnPropertyChanged(nameof(SubTotal));
        OnPropertyChanged(nameof(DebtAmount));
        OnPropertyChanged(nameof(HasDebt));
    }

    partial void OnDiscountAmountChanged(decimal value) => RaiseTotals();
    partial void OnPaidCashChanged(decimal value) => RaiseTotals();
    partial void OnPaidCardChanged(decimal value) => RaiseTotals();

    [RelayCommand]
    private async Task SubmitAsync()
    {
        var picked = Lines.Where(l => l.Quantity > 0).ToList();
        if (picked.Count == 0) { _toast.Warning(L["error"]); return; }
        try
        {
            var result = await _api.SettleAsync(_caseId, new SettleTradeCaseRequest(
                PaidCash,
                PaidCard,
                0,
                Lines: picked.Select(l => new TradeCaseSettlementLineRequest(l.Line.GoodsIssueLineId, l.Quantity)).ToList(),
                DiscountAmount: DiscountAmount,
                UseCustomerAdvance: UseCustomerAdvance,
                CloseWhenEmpty: CloseWhenEmpty,
                Note: string.IsNullOrWhiteSpace(Note) ? null : Note.Trim(),
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

public partial class SettleLineRow : ObservableObject
{
    private readonly Action _onChanged;

    public SettleLineRow(TradeCaseLineDto line, Action onChanged)
    {
        Line = line;
        _onChanged = onChanged;
        _quantity = line.CustodyQuantity;
    }

    public TradeCaseLineDto Line { get; }
    public decimal CustodyQuantity => Line.CustodyQuantity;

    [ObservableProperty] private decimal _quantity;

    public decimal LineTotal => Quantity * Line.UnitPrice;

    partial void OnQuantityChanged(decimal value)
    {
        OnPropertyChanged(nameof(LineTotal));
        _onChanged();
    }
}
