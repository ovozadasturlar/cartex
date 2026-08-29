using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Barcodes;
using Cartex.Shared.Models.Rates;
using Cartex.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.UI.ViewModels;

public sealed class BarcodeLabelChoice(string code, decimal packQty, string label)
{
    public string Code { get; } = code;
    public decimal PackQty { get; } = packQty;
    public string Label { get; } = label;
    public bool IsPack => PackQty > 1;
}

public sealed record BarcodeLabelTarget(
    long VariantId,
    string ProductName,
    string UnitName,
    string? ImageUrl,
    string? Sku,
    decimal? Price,
    string? CurrencyCode,
    string? CurrencySymbol = null,
    string? CurrencySymbolPosition = null,
    int? CurrencyDecimalDigits = null);

public partial class BarcodeLabelSession(IBarcodesApi barcodesApi, IRatesApi ratesApi, ISettingsApi settingsApi, IBarcodeLabelService labels, IPrinterService printer, PrintDispatchService dispatch, IToastService toast) : ObservableObject
{
    private long _variantId;

    public ObservableCollection<BarcodeLabelChoice> Barcodes { get; } = [];

    [ObservableProperty] private bool _isOpen;
    [ObservableProperty] private string _productName = string.Empty;
    [ObservableProperty] private string _unitName = string.Empty;
    [ObservableProperty] private string? _imageUrl;
    [ObservableProperty] private string _priceText = string.Empty;
    [ObservableProperty] private string? _sku;
    [ObservableProperty] private string? _code;
    [ObservableProperty] private int _quantity = 1;
    [ObservableProperty] private Bitmap? _preview;
    [ObservableProperty] private bool _printWithPrice;
    [ObservableProperty] private bool _printWithSku;
    [ObservableProperty] private BarcodeLabelChoice? _selectedBarcode;

    public bool HasManyBarcodes => Barcodes.Count > 1;
    public bool CanOverridePrice => printer.GetSettings().LabelAllowPriceOverride;

    public event Action? FocusBarcodesRequested;
    public event Action? FocusQuantityRequested;

    public async Task OpenAsync(BarcodeLabelTarget target)
    {
        try { printer.CacheBarcodeLabelSettings(await settingsApi.GetBarcodeLabelAsync()); }
        catch { }
        _variantId = target.VariantId;
        ProductName = target.ProductName;
        UnitName = target.UnitName;
        ImageUrl = target.ImageUrl;
        Sku = target.Sku;
        IReadOnlyList<CurrencyDto> currencies;
        try { currencies = await ratesApi.GetCurrenciesAsync(); }
        catch { currencies = []; }
        PriceText = BarcodeLabelFormatting.FormatProductPrice(
            target.Price,
            target.CurrencyCode,
            target.CurrencySymbol,
            target.CurrencySymbolPosition,
            target.CurrencyDecimalDigits,
            currencies,
            printer.GetSettings());
        Quantity = 1;
        PrintWithPrice = printer.GetSettings().LabelDefaultWithPrice;
        PrintWithSku = printer.GetSettings().LabelShowSku;
        OnPropertyChanged(nameof(CanOverridePrice));
        Code = null;
        Preview = null;
        SelectedBarcode = null;
        Barcodes.Clear();
        OnPropertyChanged(nameof(HasManyBarcodes));
        IsOpen = true;

        try
        {
            var items = await barcodesApi.GetByVariantAsync(target.VariantId);
            if (_variantId != target.VariantId || !IsOpen) return;

            if (items.Count == 0)
            {
                var generated = await barcodesApi.GenerateAsync(target.VariantId);
                items = [new BarcodeDto(0, generated, 1)];
            }

            foreach (var item in items.OrderBy(item => item.PackQty))
                Barcodes.Add(new BarcodeLabelChoice(item.Code, item.PackQty,
                    item.PackQty > 1 ? $"×{item.PackQty:0.###}" : LocalizationManager.Instance["unit_piece"]));

            SelectedBarcode = Barcodes.FirstOrDefault();
            OnPropertyChanged(nameof(HasManyBarcodes));
            if (HasManyBarcodes) FocusBarcodesRequested?.Invoke();
            else FocusQuantityRequested?.Invoke();
        }
        catch (Exception ex)
        {
            toast.Error(ApiErrors.Describe(ex));
        }
    }

    public void SetImageUrl(long variantId, string? imageUrl)
    {
        if (_variantId == variantId && IsOpen) ImageUrl = imageUrl;
    }

    partial void OnSelectedBarcodeChanged(BarcodeLabelChoice? value)
    {
        Code = value?.Code;
        UpdatePreview();
    }

    partial void OnPrintWithPriceChanged(bool value) => UpdatePreview();
    partial void OnPrintWithSkuChanged(bool value) => UpdatePreview();

    private void UpdatePreview()
    {
        if (SelectedBarcode is not { } barcode)
        {
            Preview = null;
            return;
        }

        var name = barcode.IsPack ? $"{ProductName} {barcode.Label}" : ProductName;
        try
        {
            var options = LabelSize.Resolve(printer.GetSettings()) with { ShowSku = PrintWithSku };
            var result = labels.RenderLabelPreview(
                barcode.Code,
                name,
                PrintWithPrice ? PriceText : null,
                options,
                Sku);
            using var stream = new MemoryStream(result.Image);
            Preview = new Bitmap(stream);
        }
        catch
        {
            Preview = null;
        }
    }

    partial void OnQuantityChanged(int value)
    {
        if (value < 1) Quantity = 1;
    }

    [RelayCommand]
    private void Increment() => Quantity++;

    [RelayCommand]
    private void Decrement() => Quantity = Math.Max(1, Quantity - 1);

    [RelayCommand]
    private void FocusQuantity() => FocusQuantityRequested?.Invoke();

    [RelayCommand]
    private void Cancel() => IsOpen = false;

    [RelayCommand]
    private async Task PrintAsync()
    {
        if (string.IsNullOrWhiteSpace(Code) || Quantity < 1) return;

        try
        {
            var name = SelectedBarcode is { IsPack: true } barcode ? $"{ProductName} {barcode.Label}" : ProductName;
            await dispatch.PrintBarcodeAsync(Code, name, Quantity, PrintWithPrice ? PriceText : null, Sku, PrintWithPrice, PrintWithSku);
            IsOpen = false;
        }
        catch (Exception ex)
        {
            toast.Error(ApiErrors.Describe(ex));
        }
    }
}
