using System.Collections.ObjectModel;
using System.IO;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Products;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class BarcodePrintViewModel : ViewModelBase, ILoadable
{
    private readonly IProductsApi _productsApi;
    private readonly IBarcodesApi _barcodesApi;
    private readonly IBarcodeLabelService _labels;
    private readonly IPrinterService _printer;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;

    public ObservableCollection<ProductDto> Products { get; } = [];
    public ObservableCollection<string> BarcodeOptions { get; } = [];

    [ObservableProperty] private string _search = "";
    [ObservableProperty] private ProductDto? _selectedProduct;
    [ObservableProperty] private string? _currentCode;
    [ObservableProperty] private int _quantity = 1;
    [ObservableProperty] private Bitmap? _preview;

    public bool HasCode => !string.IsNullOrWhiteSpace(CurrentCode);
    public bool HasSelection => SelectedProduct is not null;
    public string PrinterInfo => _printer.BarcodePrinter ?? L["printer_not_set"];

    public BarcodePrintViewModel(IProductsApi productsApi, IBarcodesApi barcodesApi, IBarcodeLabelService labels, IPrinterService printer, IToastService toast, IBusyService busy)
    {
        _productsApi = productsApi;
        _barcodesApi = barcodesApi;
        _labels = labels;
        _printer = printer;
        _toast = toast;
        _busy = busy;
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
            var response = await _productsApi.GetPagedAsync(1, 50, null, false, string.IsNullOrWhiteSpace(Search) ? null : Search.Trim());
            Products.Clear();
            foreach (var p in response.Content ?? []) Products.Add(p);
            if (Products.Count == 1) SelectedProduct = Products[0];
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    partial void OnSelectedProductChanged(ProductDto? value)
    {
        BarcodeOptions.Clear();
        foreach (var code in value?.Barcodes ?? []) BarcodeOptions.Add(code);
        CurrentCode = BarcodeOptions.FirstOrDefault();
        OnPropertyChanged(nameof(HasCode));
        OnPropertyChanged(nameof(HasSelection));
    }

    partial void OnCurrentCodeChanged(string? value)
    {
        OnPropertyChanged(nameof(HasCode));
        UpdatePreview();
    }

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

    [RelayCommand]
    private async Task GenerateAsync()
    {
        if (SelectedProduct is null) { _toast.Warning(L["error"]); return; }
        try
        {
            string code;
            using (_busy.Begin(L["loading"]))
                code = await _barcodesApi.GenerateAsync(SelectedProduct.DefaultVariantId);
            BarcodeOptions.Add(code);
            CurrentCode = code;
            _toast.Success(L["success"]);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task PrintAsync()
    {
        if (SelectedProduct is null || Quantity < 1) { _toast.Warning(L["error"]); return; }
        try
        {
            if (string.IsNullOrWhiteSpace(CurrentCode))
                CurrentCode = await _barcodesApi.GenerateAsync(SelectedProduct.DefaultVariantId);

            _labels.PrintLabels(CurrentCode!, SelectedProduct.Name, Quantity, null);
            _toast.Success(L["success"]);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }
}
