using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Irihi.Avalonia.Shared.Contracts;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Stocks;
using Cartex.Shared.Models.TradeCases;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class GoodsIssueLineRow : ObservableObject
{
    private readonly Action _onChanged;

    public GoodsIssueLineRow(StockOnHandDto product, Action onChanged)
    {
        _onChanged = onChanged;
        VariantId = product.VariantId;
        ProductName = product.ProductName;
        UnitName = product.UnitName;
        Stock = product.Quantity;
        UnitPrice = product.SellingPrice;
        AllowsFractional = product.AllowsFractional;
    }

    public long VariantId { get; }
    public string ProductName { get; }
    public string UnitName { get; }
    public decimal Stock { get; }
    public bool AllowsFractional { get; }
    public string QtyFormat => AllowsFractional ? "0.###" : "0";

    [ObservableProperty] private decimal _quantity = 1;
    [ObservableProperty] private decimal _unitPrice;

    public decimal LineTotal => Quantity * UnitPrice;

    partial void OnQuantityChanged(decimal value)
    {
        OnPropertyChanged(nameof(LineTotal));
        _onChanged();
    }

    partial void OnUnitPriceChanged(decimal value)
    {
        OnPropertyChanged(nameof(LineTotal));
        _onChanged();
    }
}

public partial class GoodsIssueViewModel : ViewModelBase, IDialogContext
{
    private readonly long _caseId;
    private readonly long _warehouseId;
    private readonly int _caseVersion;
    private readonly ITradeCasesApi _api;
    private readonly IStocksApi _stocksApi;
    private readonly IToastService _toast;

    public GoodsIssueViewModel(long caseId, long warehouseId, string currency, int caseVersion,
        ITradeCasesApi api, IStocksApi stocksApi, IToastService toast)
    {
        _caseId = caseId;
        _warehouseId = warehouseId;
        Currency = currency;
        _caseVersion = caseVersion;
        _api = api;
        _stocksApi = stocksApi;
        _toast = toast;
    }

    public string Currency { get; }

    public ObservableCollection<StockOnHandDto> ProductResults { get; } = [];
    public ObservableCollection<GoodsIssueLineRow> Lines { get; } = [];

    [ObservableProperty] private string _productSearch = "";
    private CancellationTokenSource? _searchCts;

    public bool HasLines => Lines.Count > 0;
    public bool HasResults => ProductResults.Count > 0;
    public decimal Total => Lines.Sum(l => l.LineTotal);

    private void RaiseTotals()
    {
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(HasLines));
    }

    partial void OnProductSearchChanged(string value)
    {
        _searchCts?.Cancel();
        var cts = _searchCts = new CancellationTokenSource();
        _ = DebouncedSearchAsync(cts.Token);
    }

    private async Task DebouncedSearchAsync(CancellationToken token)
    {
        try { await Task.Delay(300, token); } catch { return; }
        if (token.IsCancellationRequested) return;
        var term = ProductSearch.Trim();
        ProductResults.Clear();
        OnPropertyChanged(nameof(HasResults));
        if (term.Length < 2) return;
        try
        {
            var page = await _stocksApi.GetOnHandAsync(_warehouseId, search: term, page: 1, pageSize: 20, forSale: true);
            if (token.IsCancellationRequested) return;
            foreach (var p in page.Items) ProductResults.Add(p);
            OnPropertyChanged(nameof(HasResults));
        }
        catch { }
    }

    [RelayCommand]
    private void AddProduct(StockOnHandDto product)
    {
        if (Lines.Any(l => l.VariantId == product.VariantId)) return;
        Lines.Add(new GoodsIssueLineRow(product, RaiseTotals));
        ProductSearch = "";
        ProductResults.Clear();
        OnPropertyChanged(nameof(HasResults));
        RaiseTotals();
    }

    [RelayCommand]
    private void RemoveLine(GoodsIssueLineRow line)
    {
        Lines.Remove(line);
        RaiseTotals();
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (Lines.Count == 0) { _toast.Warning(L["tc_add_products"]); return; }
        if (Lines.Any(l => l.Quantity <= 0)) { _toast.Warning(L["error"]); return; }
        try
        {
            var result = await _api.IssueAsync(_caseId, new CreateGoodsIssueRequest(
                Lines.Select(l => new GoodsIssueLineRequest(l.VariantId, l.Quantity, l.UnitPrice)).ToList(),
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
