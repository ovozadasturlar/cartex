using System.Collections.ObjectModel;
using System.IO;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Querying;
using Cartex.Shared.Models.Products;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class BarcodeChoice(string code, decimal packQty, string display) : ObservableObject
{
    public string Code { get; } = code;
    public decimal PackQty { get; } = packQty;
    public string Display { get; } = display;
    [ObservableProperty] private bool _isSelected;
}

public partial class BarcodePrintViewModel : ViewModelBase, ILoadable
{
    private readonly IProductsApi _productsApi;
    private readonly IBarcodesApi _barcodesApi;
    private readonly IBarcodeLabelService _labels;
    private readonly IPrinterService _printer;
    private readonly IToastService _toast;

    public ObservableCollection<ProductDto> Products { get; } = [];
    public ObservableCollection<BarcodeChoice> BarcodeOptions { get; } = [];

    [ObservableProperty] private string _search = "";
    [ObservableProperty] private ProductDto? _selectedProduct;
    [ObservableProperty] private string? _currentCode;
    [ObservableProperty] private int _quantity = 1;
    [ObservableProperty] private Bitmap? _preview;

    public bool HasCode => !string.IsNullOrWhiteSpace(CurrentCode);
    public bool HasSelection => SelectedProduct is not null;
    public bool HasBarcodes => BarcodeOptions.Count > 0;
    public string PrinterInfo => _printer.BarcodePrinter ?? L["printer_not_set"];

    public BarcodePrintViewModel(IProductsApi productsApi, IBarcodesApi barcodesApi, IBarcodeLabelService labels, IPrinterService printer, IToastService toast)
    {
        _productsApi = productsApi;
        _barcodesApi = barcodesApi;
        _labels = labels;
        _printer = printer;
        _toast = toast;
    }

    public Task LoadAsync()
    {
        OnPropertyChanged(nameof(PrinterInfo));
        return SearchAsync();
    }

    private CancellationTokenSource? _searchCts;

    partial void OnSearchChanged(string value)
    {
        _searchCts?.Cancel();
        var cts = _searchCts = new CancellationTokenSource();
        _ = DebouncedSearchAsync(cts.Token);
    }

    private async Task DebouncedSearchAsync(CancellationToken token)
    {
        try { await Task.Delay(300, token); } catch { return; }
        if (!token.IsCancellationRequested) await SearchAsync();
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        try
        {
            var response = await _productsApi.QueryAsync(QueryRequest.Create().Page(1, 50).Search(Search).Build());
            Products.Clear();
            foreach (var p in response.Content ?? []) Products.Add(p);
            if (Products.Count == 1) SelectedProduct = Products[0];
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    partial void OnSelectedProductChanged(ProductDto? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        _ = LoadBarcodesAsync(value);
    }

    private async Task LoadBarcodesAsync(ProductDto? product)
    {
        BarcodeOptions.Clear();
        CurrentCode = null;
        OnPropertyChanged(nameof(HasBarcodes));
        if (product is null) return;
        try
        {
            var barcodes = await _barcodesApi.GetByVariantAsync(product.DefaultVariantId);
            if (!ReferenceEquals(SelectedProduct, product)) return;
            foreach (var b in barcodes)
                BarcodeOptions.Add(new BarcodeChoice(b.Code, b.PackQty,
                    b.PackQty <= 1 ? L["barcode_single"] : string.Format(L["barcode_pack_fmt"], b.PackQty.ToString("0.##"))));
            if (BarcodeOptions.Count > 0) SelectBarcode(BarcodeOptions[0]);
            OnPropertyChanged(nameof(HasBarcodes));
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private void SelectBarcode(BarcodeChoice choice)
    {
        foreach (var b in BarcodeOptions) b.IsSelected = ReferenceEquals(b, choice);
        CurrentCode = choice.Code;
    }

    partial void OnCurrentCodeChanged(string? value)
    {
        OnPropertyChanged(nameof(HasCode));
        PrintCommand.NotifyCanExecuteChanged();
        UpdatePreview();
    }

    partial void OnQuantityChanged(int value)
    {
        if (value < 1) Quantity = 1;
    }

    [RelayCommand] private void Increment() => Quantity++;
    [RelayCommand] private void Decrement() { if (Quantity > 1) Quantity--; }

    private void UpdatePreview()
    {
        if (string.IsNullOrWhiteSpace(CurrentCode)) { Preview = null; return; }
        try
        {
            var png = _labels.RenderPng(CurrentCode);
            using var stream = new MemoryStream(png);
            Preview = new Bitmap(stream);
        }
        catch { Preview = null; }
    }

    [RelayCommand(CanExecute = nameof(HasCode))]
    private void Print()
    {
        if (SelectedProduct is null || string.IsNullOrWhiteSpace(CurrentCode) || Quantity < 1) { _toast.Warning(L["error"]); return; }
        try
        {
            _labels.PrintLabels(CurrentCode, SelectedProduct.Name, Quantity, null);
            _toast.Success(L["success"]);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }
}
