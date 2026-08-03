using System.Collections.ObjectModel;
using System.IO;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Paging;
using Cartex.ApiClient.Querying;
using Cartex.Shared.Models.Common;
using Cartex.Shared.Models.Products;
using Cartex.UI.Models;
using Cartex.UI.Services;
using Cartex.UI.ViewModels.Common;

namespace Cartex.UI.ViewModels;

public sealed class BarcodeChoice(string code, decimal packQty, string display)
{
    public string Code { get; } = code;
    public decimal PackQty { get; } = packQty;
    public string Display { get; } = display;
    public bool IsPack => PackQty > 1;
}

public partial class BarcodePrintViewModel : ViewModelBase, ILoadable
{
    private readonly IProductsApi _productsApi;
    private readonly IBarcodesApi _barcodesApi;
    private readonly IBarcodeLabelService _labels;
    private readonly IPrinterService _printer;
    private readonly IToastService _toast;

    public PaginationState Paging { get; } = new();
    public ObservableCollection<ProductDto> Products { get; } = [];
    public ObservableCollection<BarcodeChoice> BarcodeOptions { get; } = [];

    [ObservableProperty] private string _search = "";
    [ObservableProperty] private ProductDto? _selectedProduct;
    [ObservableProperty] private BarcodeChoice? _selectedBarcode;
    [ObservableProperty] private string? _currentCode;
    [ObservableProperty] private int _quantity = 1;
    [ObservableProperty] private Bitmap? _preview;
    [ObservableProperty] private bool _printWithPrice;

    public event Action? FocusChipsRequested;
    public event Action? FocusQuantityRequested;

    public bool HasCode => !string.IsNullOrWhiteSpace(CurrentCode);
    public bool HasSelection => SelectedProduct is not null;
    public bool HasBarcodes => BarcodeOptions.Count > 0;
    public string PrinterInfo => _printer.BarcodePrinter ?? L["printer_not_set"];
    public string PriceText => SelectedProduct?.SellingPrice is { } price
        ? BarcodeLabelFormatting.FormatPrice(
            price,
            SelectedProduct.PriceCurrency,
            SelectedProduct.PriceSymbol,
            SelectedProduct.PriceSymbolPosition,
            SelectedProduct.PriceDecimalDigits,
            _printer.GetSettings())
        : "";
    public string SelectedProductImageUrl => SelectedProduct?.ImageUrl ?? string.Empty;
    public string SelectedProductName => SelectedProduct?.Name ?? string.Empty;
    public string SelectedProductUnitName => SelectedProduct?.UnitName ?? string.Empty;

    private IReadOnlyList<PageShortcut>? _shortcuts;

    public IReadOnlyList<PageShortcut> Shortcuts => _shortcuts ??=
    [
        new(Key.Enter, KeyModifiers.Control, "shortcut_print", () => PrintCommand.Execute(null), () => HasCode, WorksInText: true),
    ];

    public BarcodePrintViewModel(IProductsApi productsApi, IBarcodesApi barcodesApi, IBarcodeLabelService labels, IPrinterService printer, IToastService toast)
    {
        _productsApi = productsApi;
        _barcodesApi = barcodesApi;
        _labels = labels;
        _printer = printer;
        _toast = toast;
        Paging.Attach(LoadProductsAsync);
    }

    public Task LoadAsync()
    {
        PrintWithPrice = _printer.GetSettings().LabelDefaultWithPrice;
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
        if (token.IsCancellationRequested) return;
        Paging.Page = 1;
        await LoadProductsAsync();
    }

    [RelayCommand]
    private Task SearchAsync()
    {
        Paging.Page = 1;
        return LoadProductsAsync();
    }

    private async Task LoadProductsAsync()
    {
        try
        {
            var response = await _productsApi.QueryAsync(QueryRequest.Create().Page(Paging.Page, Paging.PageSize).Search(Search).Build());
            var paged = response.ToPaged();
            Products.Clear();
            foreach (var p in paged.Items) Products.Add(p);
            Paging.Apply(paged.Meta);
            if (paged.Meta.TotalCount == 1) SelectedProduct = Products[0];
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    partial void OnSelectedProductChanged(ProductDto? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(PriceText));
        OnPropertyChanged(nameof(SelectedProductImageUrl));
        OnPropertyChanged(nameof(SelectedProductName));
        OnPropertyChanged(nameof(SelectedProductUnitName));
        _ = LoadBarcodesAsync(value);
    }

    private async Task LoadBarcodesAsync(ProductDto? product)
    {
        BarcodeOptions.Clear();
        SelectedBarcode = null;
        OnPropertyChanged(nameof(HasBarcodes));
        if (product is null) return;
        try
        {
            var barcodes = await _barcodesApi.GetByVariantAsync(product.DefaultVariantId);
            if (!ReferenceEquals(SelectedProduct, product)) return;
            foreach (var b in barcodes)
                BarcodeOptions.Add(new BarcodeChoice(b.Code, b.PackQty,
                    b.PackQty <= 1 ? L["barcode_single"] : string.Format(L["barcode_pack_fmt"], b.PackQty.ToString("0.##"))));
            if (BarcodeOptions.Count > 0) SelectedBarcode = BarcodeOptions[0];
            OnPropertyChanged(nameof(HasBarcodes));
            if (BarcodeOptions.Count > 1) FocusChipsRequested?.Invoke();
            else if (BarcodeOptions.Count == 1) FocusQuantityRequested?.Invoke();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    partial void OnSelectedBarcodeChanged(BarcodeChoice? value) => CurrentCode = value?.Code;

    [RelayCommand] private void FocusQuantity() => FocusQuantityRequested?.Invoke();

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

    partial void OnPrintWithPriceChanged(bool value)
    {
        OnPropertyChanged(nameof(PriceText));
        UpdatePreview();
    }

    [RelayCommand] private void Increment() => Quantity++;
    [RelayCommand] private void Decrement() { if (Quantity > 1) Quantity--; }
    [RelayCommand] private void ClearSelection() => SelectedProduct = null;

    private void UpdatePreview()
    {
        if (SelectedProduct is null || string.IsNullOrWhiteSpace(CurrentCode)) { Preview = null; return; }
        try
        {
            var name = SelectedBarcode is { IsPack: true } barcode ? $"{SelectedProduct.Name} {barcode.Display}" : SelectedProduct.Name;
            var png = _labels.RenderLabelPreview(CurrentCode, name, PrintWithPrice ? PriceText : null, SelectedProduct.Code);
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
            var name = SelectedBarcode is { IsPack: true } barcode ? $"{SelectedProduct.Name} {barcode.Display}" : SelectedProduct.Name;
            _labels.PrintLabels(CurrentCode, name, Quantity, null, PrintWithPrice ? PriceText : null, SelectedProduct.Code);
            _toast.Success(L["success"]);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }
}
