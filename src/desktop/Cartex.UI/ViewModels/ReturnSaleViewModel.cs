using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Irihi.Avalonia.Shared.Contracts;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Sales;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public sealed record ReturnSaleSummaryInfo(
    long SaleId,
    string ReceiptToken,
    DateTime SaleDate,
    string UserName,
    string? CustomerName,
    decimal TotalAmount,
    decimal PaidCash,
    decimal PaidCard,
    decimal PaidBonus,
    decimal DebtAmount);

public partial class ReturnSaleViewModel : ViewModelBase, IDialogContext
{
    private readonly long _saleId;
    private readonly ISalesApi _salesApi;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;

    public ReturnSaleViewModel(long saleId, ISalesApi salesApi, IToastService toast, IBusyService busy,
        SaleDto? preloadedSale = null, SaleDetailDto? preloadedDetail = null)
    {
        _saleId = saleId;
        _salesApi = salesApi;
        _toast = toast;
        _busy = busy;

        if (preloadedSale is not null)
            LoadFromSale(preloadedSale);
        else if (preloadedDetail is not null)
            LoadFromDetail(preloadedDetail);
    }

    [ObservableProperty] private ReturnSaleSummaryInfo? _sale;
    [ObservableProperty] private decimal _totalReturnAmount;
    [ObservableProperty] private int _totalReturnItemCount;
    [ObservableProperty] private bool _isLoading;

    public ObservableCollection<ReturnLineItem> ReturnLines { get; } = [];

    public async Task InitAsync()
    {
        if (Sale is not null && ReturnLines.Count > 0) return;
        try
        {
            IsLoading = true;
            using (_busy.Begin(L["loading"]))
            {
                var detail = await _salesApi.GetByIdAsync(_saleId);
                LoadFromDetail(detail);
            }
        }
        catch (Exception ex)
        {
            _toast.Error(ApiErrors.Describe(ex));
            RequestClose?.Invoke(this, false);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void LoadFromSale(SaleDto sale)
    {
        Sale = new ReturnSaleSummaryInfo(
            sale.Id,
            sale.ReceiptToken,
            sale.SaleDate,
            sale.UserName,
            sale.CustomerName,
            sale.TotalAmount,
            sale.PaidCash,
            sale.PaidCard,
            sale.PaidBonus,
            sale.DebtAmount);

        ReturnLines.Clear();
        foreach (var i in sale.Items)
        {
            var remaining = i.Quantity - i.ReturnedQuantity;
            if (remaining > 0)
                ReturnLines.Add(new ReturnLineItem(i.SaleItemId, i.ProductName, remaining, i.UnitPrice, RecalculateTotals));
        }
        RecalculateTotals();
    }

    private void LoadFromDetail(SaleDetailDto detail)
    {
        Sale = new ReturnSaleSummaryInfo(
            detail.Id,
            detail.ReceiptToken,
            detail.SaleDate,
            detail.UserName,
            detail.CustomerName,
            detail.TotalAmount,
            detail.PaidCash,
            detail.PaidCard,
            detail.PaidBonus,
            detail.DebtAmount);

        ReturnLines.Clear();
        foreach (var i in detail.Items)
        {
            var remaining = i.ReturnableQuantity > 0 ? i.ReturnableQuantity : (i.Quantity - i.ReturnedQuantity);
            if (remaining > 0)
                ReturnLines.Add(new ReturnLineItem(i.SaleItemId, i.ProductName, remaining, i.UnitPrice, RecalculateTotals));
        }
        RecalculateTotals();
    }

    public void RecalculateTotals()
    {
        TotalReturnAmount = ReturnLines.Sum(l => l.LineTotal);
        TotalReturnItemCount = ReturnLines.Count(l => l.Quantity > 0);
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var line in ReturnLines)
            line.Quantity = line.Remaining;
        RecalculateTotals();
    }

    [RelayCommand]
    private void Clear()
    {
        foreach (var line in ReturnLines)
            line.Quantity = 0;
        RecalculateTotals();
    }

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        if (Sale is null) return;
        var lines = ReturnLines
            .Where(l => l.Quantity > 0)
            .Select(l => new ReturnLineRequest(l.SaleItemId, l.Quantity, l.Restock, l.Reason))
            .ToList();
        if (lines.Count == 0)
        {
            _toast.Error(L["return_select_qty"]);
            return;
        }

        try
        {
            using (_busy.Begin(L["loading"]))
                await _salesApi.ReturnAsync(Sale.SaleId, new ReturnSaleRequest(Sale.SaleId, lines));
            _toast.Success(L["success"]);
            RequestClose?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            _toast.Error(ApiErrors.Describe(ex));
        }
    }

    public event EventHandler<object?>? RequestClose;

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(this, false);

    public void Close() => RequestClose?.Invoke(this, false);
}

public partial class ReturnLineItem : ObservableObject
{
    private readonly Action _onQuantityChanged;

    public ReturnLineItem(long saleItemId, string productName, decimal remaining, decimal unitPrice, Action onQuantityChanged)
    {
        SaleItemId = saleItemId;
        ProductName = productName;
        Remaining = remaining;
        UnitPrice = unitPrice;
        _quantity = remaining;
        _onQuantityChanged = onQuantityChanged;
    }

    public long SaleItemId { get; }
    public string ProductName { get; }
    public decimal Remaining { get; }
    public decimal UnitPrice { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LineTotal))]
    private decimal _quantity;

    [ObservableProperty] private bool _restock = true;
    [ObservableProperty] private string? _reason;

    public decimal LineTotal => Quantity * UnitPrice;

    partial void OnQuantityChanged(decimal value)
    {
        _onQuantityChanged?.Invoke();
    }
}
