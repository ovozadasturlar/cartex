using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Querying;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;
using Cartex.Shared.Models.Auth;
using Cartex.Shared.Models.Barcodes;
using Cartex.Shared.Models.Common;
using Cartex.Shared.Models.Products;
using Cartex.Shared.Models.Rates;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace Cartex.Mobile.Store.ViewModels;

public partial class ScanViewModel : ObservableObject
{
    private const int SearchPageSize = 30;

    private readonly ISessionsApi _sessionsApi;
    private readonly IProductsApi _productsApi;
    private readonly IRatesApi _ratesApi;
    private readonly IBarcodesApi _barcodesApi;
    private readonly WarehouseContext _warehouse;
    private readonly MobilePermissions _permissions;
    private readonly CartStore _cart;
    private readonly SupplyCartStore _supplyCart;
    private readonly ImageUrlBuilder _images;
    private readonly MobilePrintDispatcher _printDispatcher;
    private readonly BarcodeLabelSettingsCache _labelSettings;

    [ObservableProperty] private bool _isDetecting = true;
    [ObservableProperty] private string? _status = Loc.Instance["scan_hint_store"];
    [ObservableProperty] private bool _overlayVisible;
    [ObservableProperty] private bool _unknownBarcodeVisible;
    [ObservableProperty] private string _unknownBarcode = "";
    [ObservableProperty] private string _productName = "";
    [ObservableProperty] private string _priceText = "";
    [ObservableProperty] private string _stockText = "";
    [ObservableProperty] private string? _imageUrl;
    [ObservableProperty] private decimal _quantity;
    [ObservableProperty] private bool _canEditProduct;
    [ObservableProperty] private bool _canReceiveStock;
    [ObservableProperty] private int _cartCount;
    [ObservableProperty] private int _supplyCartCount;
    [ObservableProperty] private bool _searchVisible;
    [ObservableProperty] private bool _isSearching;
    [ObservableProperty] private bool _isLoadingMoreResults;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private bool _productActionsExpanded;
    [ObservableProperty] private bool _isBarcodeMode;
    [ObservableProperty] private bool _isPrintingBarcode;
    [ObservableProperty] private BarcodeChoice? _selectedBarcode;
    [ObservableProperty] private int _printCopies = 1;
    [ObservableProperty] private bool _printWithPrice;
    [ObservableProperty] private bool _canOverridePrintPrice = true;

    public ObservableCollection<SearchRow> SearchResults { get; } = [];
    public ObservableCollection<BarcodeChoice> BarcodeChoices { get; } = [];
    public bool CanPrintBarcode => _printDispatcher.CanPrintBarcode;
    public bool HasProductActions => CanEditProduct || CanPrintBarcode;
    public string PrintTotalText => $"{Loc.Instance["total"]}: {PrintCopies}";
    public string BarcodePreviewPrice => PrintWithPrice && _product is not null
        ? FormatLabelPrice(_product, _labelSettings.Current)
        : string.Empty;
    public string BarcodePreviewSku => _labelSettings.Current.ShowSku ? _productSku ?? string.Empty : string.Empty;
    public int BarcodePreviewNameLines => _labelSettings.Current.NameLines;

    private ProductLookupDto? _product;
    private decimal _step = 1;
    private bool _handled;
    private string? _lastValue;
    private DateTime _lastAt = DateTime.MinValue;
    private CancellationTokenSource? _searchCts;
    private string? _activeSearch;
    private int _loadedSearchPage;
    private bool _hasMoreSearchResults;
    private IReadOnlyList<CurrencyDto>? _currencies;
    private string? _activeBarcode;
    private string? _productSku;

    public ScanViewModel(ISessionsApi sessionsApi, IProductsApi productsApi, IRatesApi ratesApi, IBarcodesApi barcodesApi, WarehouseContext warehouse, MobilePermissions permissions, CartStore cart, SupplyCartStore supplyCart, ImageUrlBuilder images, MobilePrintDispatcher printDispatcher, BarcodeLabelSettingsCache labelSettings)
    {
        _sessionsApi = sessionsApi;
        _productsApi = productsApi;
        _ratesApi = ratesApi;
        _barcodesApi = barcodesApi;
        _warehouse = warehouse;
        _permissions = permissions;
        _cart = cart;
        _supplyCart = supplyCart;
        _images = images;
        _printDispatcher = printDispatcher;
        _labelSettings = labelSettings;
        _cartCount = cart.Count;
        _supplyCartCount = supplyCart.Count;
        cart.Changed += () => CartCount = _cart.Count;
        supplyCart.Changed += OnSupplyCartChanged;
        WeakReferenceMessenger.Default.Register<ScanViewModel, ProductChangedMessage>(this, static (recipient, message) => _ = recipient.RefreshProductAsync(message.Value));
        CanEditProduct = permissions.Has("products.edit");
        CanReceiveStock = permissions.Has("supplies.create");
    }

    public async Task HandleAsync(string value)
    {
        if (_handled || OverlayVisible || UnknownBarcodeVisible || SearchVisible) return;
        if (value == _lastValue && (DateTime.UtcNow - _lastAt).TotalSeconds < 1.5) return;
        _handled = true;
        _lastValue = value;
        _lastAt = DateTime.UtcNow;
        IsDetecting = false;

        if (value.StartsWith("cartexqr:", StringComparison.OrdinalIgnoreCase))
            await ApproveQrAsync(value["cartexqr:".Length..]);
        else if (HandoffCode().IsMatch(value))
            await OpenHandoffAsync(value);
        else
            await LookupAsync(value);
    }

    private async Task ApproveQrAsync(string code)
    {
        var page = Shell.Current.CurrentPage;
        var confirmed = page is not null && await page.DisplayAlertAsync(
            Loc.Instance["qr_approve_title"], Loc.Instance["qr_approve_msg"], Loc.Instance["ok"], Loc.Instance["cancel"]);
        if (!confirmed)
        {
            Resume();
            return;
        }
        try
        {
            Status = Loc.Instance["approving"];
            await _sessionsApi.ApproveQrAsync(new ApproveQrLoginRequest(code));
            Ui.Toast(Loc.Instance["qr_approved"]);
            Resume();
        }
        catch (Refit.ApiException ex)
        {
            await FlashAsync(string.Format(Loc.Instance["err_server_fmt"], (int)ex.StatusCode));
        }
        catch
        {
            await FlashAsync(Loc.Instance["err_no_connection"]);
        }
    }

    private async Task OpenHandoffAsync(string code)
    {
        if (_permissions.HasAny("sales.create", "sales.checkout"))
            await Shell.Current.GoToAsync($"checkout?code={code}");
        else
            Ui.Toast(Loc.Instance["err_forbidden"]);
        Resume();
    }

    private async Task LookupAsync(string barcode)
    {
        if (!await _warehouse.EnsureSelectedAsync())
        {
            Ui.Toast(Loc.Instance["warehouse_none"]);
            Resume();
            return;
        }
        try
        {
            var product = await _productsApi.GetByBarcodeAsync(barcode, _warehouse.WarehouseId!.Value, forSale: false);
            ProductActionsExpanded = false;
            _product = product;
            _activeBarcode = barcode;
            _productSku = null;
            _step = product.PackQty > 0 ? product.PackQty : 1;
            Quantity = _step;
            ProductName = product.ProductName;
            PriceText = FormatPrice(product);
            StockText = StockTextFor(product.OnHand, product.UnitName, product.VariantId);
            OverlayVisible = true;
            
            ImageUrl = _images.FromKey(product.ImageKey, thumb: false);
        }
        catch (Refit.ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            await HandleUnknownBarcodeAsync(barcode);
        }
        catch (Refit.ApiException ex)
        {
            await FlashAsync(ApiErrors.Describe(ex));
        }
        catch
        {
            await FlashAsync(Loc.Instance["err_no_connection"]);
        }
    }

    private Task HandleUnknownBarcodeAsync(string barcode)
    {
        UnknownBarcode = barcode;
        UnknownBarcodeVisible = true;
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task AttachUnknownBarcodeAsync()
    {
        if (string.IsNullOrEmpty(UnknownBarcode)) return;

        var barcode = UnknownBarcode;
        UnknownBarcodeVisible = false;
        await Shell.Current.GoToAsync($"barcode_attach?barcode={Uri.EscapeDataString(barcode)}");
        Resume();
    }

    [RelayCommand]
    private async Task CreateProductFromUnknownBarcodeAsync()
    {
        if (string.IsNullOrEmpty(UnknownBarcode)) return;

        var barcode = UnknownBarcode;
        UnknownBarcodeVisible = false;
        await Shell.Current.GoToAsync($"product/edit?id=0&barcode={Uri.EscapeDataString(barcode)}");
        Resume();
    }

    [RelayCommand]
    private void CloseUnknownBarcode() => Resume();

    [RelayCommand]
    private void Increase() => Quantity += _step;

    [RelayCommand]
    private void Decrease() => Quantity = Math.Max(_step, Quantity - _step);

    [RelayCommand]
    private void AddToCart()
    {
        if (_product is null) return;
        var existing = _cart.Lines.FirstOrDefault(l => l.VariantId == _product.VariantId)?.Quantity ?? 0;
        _cart.Add(_product);
        _cart.SetQuantity(_product.VariantId, existing + Quantity);
        Ui.Toast(Loc.Instance["added_to_cart"]);
        CloseOverlay();
    }

    [RelayCommand]
    private async Task EditProductAsync()
    {
        var variantId = _product?.VariantId;
        if (variantId is null) return;
        CloseOverlay();
        await Shell.Current.GoToAsync($"product/edit?id={variantId}");
    }

    [RelayCommand]
    private async Task OpenBarcodePrintAsync()
    {
        if (_product is null || !CanPrintBarcode) return;
        try
        {
            var barcodes = await _barcodesApi.GetByVariantAsync(_product.VariantId);
            var settings = await _labelSettings.RefreshAsync();
            BarcodeChoices.Clear();
            foreach (var barcode in barcodes)
                BarcodeChoices.Add(new BarcodeChoice(barcode));
            SelectedBarcode = BarcodeChoices.FirstOrDefault(x => x.Code == _activeBarcode) ?? BarcodeChoices.FirstOrDefault();
            PrintCopies = 1;
            PrintWithPrice = settings.DefaultWithPrice;
            CanOverridePrintPrice = settings.AllowPriceOverride;
            ProductActionsExpanded = false;
            IsBarcodeMode = true;
            NotifyBarcodePreviewChanged();
        }
        catch (Exception ex)
        {
            Ui.Toast(ex is Refit.ApiException api ? ApiErrors.Describe(api) : Loc.Instance["err_no_connection"]);
        }
    }

    [RelayCommand]
    private void BackToProduct()
    {
        IsBarcodeMode = false;
        ProductActionsExpanded = false;
    }

    [RelayCommand]
    private void SelectBarcode(BarcodeChoice choice) => SelectedBarcode = choice;

    [RelayCommand]
    private void IncreasePrintCopies() => PrintCopies = Math.Min(500, PrintCopies + 1);

    [RelayCommand]
    private void DecreasePrintCopies() => PrintCopies = Math.Max(1, PrintCopies - 1);

    [RelayCommand]
    private async Task PrintBarcodeAsync()
    {
        if (_product is null || SelectedBarcode is null || IsPrintingBarcode) return;
        IsPrintingBarcode = true;
        try
        {
            await _printDispatcher.PrintBarcodeAsync(
                SelectedBarcode.Code,
                ProductName,
                PrintCopies,
                FormatLabelPrice(_product, _labelSettings.Current),
                _productSku,
                PrintWithPrice);
            Ui.Toast(Loc.Instance["print_sent"]);
        }
        catch (Exception ex)
        {
            Ui.Toast(ex is Refit.ApiException api ? ApiErrors.Describe(api) : Loc.Instance["err_no_connection"]);
        }
        finally
        {
            IsPrintingBarcode = false;
        }
    }

    [RelayCommand]
    private void ReceiveStock()
    {
        if (_product is null) return;
        var existing = _supplyCart.Lines.FirstOrDefault(l => l.VariantId == _product.VariantId)?.Quantity ?? 0;
        _supplyCart.Add(_product);
        _supplyCart.SetQuantity(_product.VariantId, existing + Quantity);
        Ui.Toast($"{Loc.Instance["receive_stock"]} ✓");
        CloseOverlay();
    }

    [RelayCommand]
    private Task OpenSupplyCart() => Shell.Current.GoToAsync("receive_cart");

    [RelayCommand]
    private void CloseOverlay()
    {
        OverlayVisible = false;
        IsBarcodeMode = false;
        ProductActionsExpanded = false;
        BarcodeChoices.Clear();
        _product = null;
        Resume();
    }

    [RelayCommand]
    private Task OpenCart() => Shell.Current.GoToAsync("cart");

    [RelayCommand]
    private void ToggleSearch()
    {
        if (!SearchVisible && !_permissions.Has("products.view"))
        {
            Ui.Toast(Loc.Instance["err_forbidden"]);
            return;
        }
        SearchVisible = !SearchVisible;
        if (!SearchVisible)
        {
            _searchCts?.Cancel();
            IsSearching = false;
            SearchText = "";
            SearchResults.Clear();
            if (!OverlayVisible)
                IsDetecting = true;
        }
        else
        {
            IsDetecting = false;
        }
    }

    [RelayCommand]
    private void ClearSearch() => SearchText = "";

    [RelayCommand]
    private async Task CreateProductFromSearchAsync()
    {
        if (!CanEditProduct) return;

        var barcode = SearchText.Trim();
        SearchVisible = false;
        SearchText = "";
        SearchResults.Clear();
        var route = string.IsNullOrWhiteSpace(barcode)
            ? "product/edit?id=0"
            : $"product/edit?id=0&barcode={Uri.EscapeDataString(barcode)}";
        await Shell.Current.GoToAsync(route);
        Resume();
    }

    partial void OnSearchTextChanged(string value)
    {
        _searchCts?.Cancel();
        SearchResults.Clear();
        _activeSearch = null;
        _loadedSearchPage = 0;
        _hasMoreSearchResults = false;
        IsLoadingMoreResults = false;

        var text = value.Trim();
        if (text.Length < 2)
        {
            IsSearching = false;
            return;
        }

        var cts = _searchCts = new CancellationTokenSource();
        _activeSearch = text;
        IsSearching = true;
        _ = SearchAsync(text, cts, 1);
    }

    private async Task SearchAsync(string text, CancellationTokenSource cts, int page)
    {
        try
        {
            await Task.Delay(200, cts.Token);
            var response = await _productsApi.QueryAsync(QueryRequest.Create().Page(page, SearchPageSize).Search(text).Build());
            if (cts.IsCancellationRequested) return;

            var products = (response.Content ?? []).ToList();
            _loadedSearchPage = page;
            _hasMoreSearchResults = products.Count == SearchPageSize;
            foreach (var p in products.Where(p => p.IsEnabled))
                SearchResults.Add(new SearchRow(p, _images));
        }
        catch (OperationCanceledException) { }
        catch { }
        finally
        {
            if (_searchCts == cts)
                IsSearching = false;
        }
    }

    [RelayCommand]
    private async Task LoadMoreResultsAsync()
    {
        if (IsSearching || IsLoadingMoreResults || !_hasMoreSearchResults || string.IsNullOrWhiteSpace(_activeSearch) || _searchCts is null)
            return;

        var cts = _searchCts;
        var page = _loadedSearchPage + 1;
        IsLoadingMoreResults = true;
        try
        {
            var response = await _productsApi.QueryAsync(QueryRequest.Create().Page(page, SearchPageSize).Search(_activeSearch).Build());
            if (cts.IsCancellationRequested || _searchCts != cts || _activeSearch is null)
                return;

            var products = (response.Content ?? []).ToList();
            _loadedSearchPage = page;
            _hasMoreSearchResults = products.Count == SearchPageSize;
            foreach (var p in products.Where(p => p.IsEnabled))
                SearchResults.Add(new SearchRow(p, _images));
        }
        catch (OperationCanceledException) { }
        catch { }
        finally
        {
            if (_searchCts == cts)
                IsLoadingMoreResults = false;
        }
    }

    [RelayCommand]
    private async Task PickResultAsync(SearchRow row)
    {
        var p = row.Product;
        ProductActionsExpanded = false;
        try
        {
            _product = await BuildLookupAsync(p, 1);
        }
        catch (InvalidOperationException ex)
        {
            Ui.Toast(ex.Message);
            return;
        }
        _step = 1;
        _activeBarcode = p.Barcodes.FirstOrDefault();
        _productSku = p.Code;
        Quantity = 1;
        ProductName = p.Name;
        PriceText = FormatPrice(_product);
        StockText = StockTextFor(p.OnHand, p.UnitName, p.DefaultVariantId);
        OverlayVisible = true;
        IsDetecting = false;
        ImageUrl = _images.FromKey(p.ImageKey, thumb: false);
        
        SearchVisible = false;
        SearchText = "";
        SearchResults.Clear();
    }

    private async Task FlashAsync(string message)
    {
        Status = message;
        await Task.Delay(1800);
        Resume();
    }

    private void OnSupplyCartChanged()
    {
        SupplyCartCount = _supplyCart.Count;
        if (_product is not null)
            StockText = StockTextFor(_product.OnHand, _product.UnitName, _product.VariantId);
    }

    private string StockTextFor(decimal onHand, string unitName, long variantId)
    {
        var pending = _supplyCart.Lines.FirstOrDefault(x => x.VariantId == variantId)?.Quantity ?? 0;
        var pendingText = pending > 0 ? $"({pending:0.###})" : "";
        return $"{Loc.Instance["stock_label"]}{onHand:0.###}{pendingText} {unitName}";
    }

    private async Task RefreshProductAsync(long variantId)
    {
        if (_product?.VariantId != variantId)
            return;

        try
        {
            var updated = (await _productsApi.GetAllAsync(variantId: variantId)).FirstOrDefault();
            if (updated is null)
                return;

            _product = await BuildLookupAsync(updated, _product.PackQty);
            ProductName = _product.ProductName;
            PriceText = FormatPrice(_product);
            StockText = StockTextFor(_product.OnHand, _product.UnitName, _product.VariantId);
            ImageUrl = _images.FromKey(_product.ImageKey, thumb: false);
        }
        catch { }
    }

    public Task RefreshVisibleProductAsync() => _product is { } product
        ? RefreshProductAsync(product.VariantId)
        : Task.CompletedTask;

    private async Task<ProductLookupDto> BuildLookupAsync(ProductDto product, decimal packQty)
    {
        _currencies ??= await _ratesApi.GetCurrenciesAsync();
        var baseCurrency = _currencies.FirstOrDefault(currency => currency.IsBase)?.Code ?? "UZS";
        var priceCurrency = product.PriceCurrency ?? baseCurrency;
        var isBase = string.Equals(priceCurrency, baseCurrency, StringComparison.OrdinalIgnoreCase);
        var rate = isBase
            ? 1m
            : _currencies.FirstOrDefault(currency => string.Equals(currency.Code, priceCurrency, StringComparison.OrdinalIgnoreCase))?.Rate
              ?? throw new InvalidOperationException(Loc.Instance["currency_rate_required"]);
        var originalPrice = product.SellingPrice ?? 0;
        var basePrice = Math.Round(originalPrice * rate, 2);
        var step = product.AllowsAmountEntry ? 0.001m : 1m;
        return new ProductLookupDto(
            product.DefaultVariantId,
            product.Name,
            product.UnitName,
            packQty,
            basePrice,
            product.OnHand,
            product.Dimension ?? "",
            product.ImageKey,
            product.AllowsAmountEntry,
            step,
            originalPrice,
            priceCurrency,
            baseCurrency,
            rate);
    }

    private static string FormatPrice(ProductLookupDto product)
    {
        var baseCurrency = product.BaseCurrency ?? "UZS";
        var priceCurrency = product.PriceCurrency ?? baseCurrency;
        var original = Money.Currency(product.OriginalSellingPrice ?? product.SellingPrice, priceCurrency);
        return string.Equals(priceCurrency, baseCurrency, StringComparison.OrdinalIgnoreCase)
            ? original
            : $"{original} ≈ {Money.Currency(product.SellingPrice, baseCurrency)}";
    }

    private static string FormatLabelPrice(ProductLookupDto product, Cartex.Shared.Models.Settings.BarcodeLabelSettingsDto settings)
    {
        var useDefaultCurrency = settings.PriceCurrencyMode == "default";
        var code = useDefaultCurrency
            ? product.BaseCurrency ?? "UZS"
            : product.PriceCurrency ?? product.BaseCurrency ?? "UZS";
        var metadata = CurrencyCatalog.Resolve(code);
        var token = settings.CurrencyDisplay == "code" ? metadata.Code : metadata.Symbol;
        token = settings.CurrencyCase switch
        {
            "upper" => token.ToUpperInvariant(),
            "lower" => token.ToLowerInvariant(),
            _ => token
        };
        var amount = useDefaultCurrency
            ? product.SellingPrice
            : product.OriginalSellingPrice ?? product.SellingPrice;
        var number = amount.ToString($"N{metadata.DecimalDigits}");
        var position = settings.CurrencyDisplay == "code" ? "Suffix" : metadata.SymbolPosition;
        return position == "Prefix" ? $"{token}{number}" : $"{number} {token}";
    }

    private void Resume()
    {
        Status = Loc.Instance["scan_hint_store"];
        _handled = false;
        _lastValue = null;
        _lastAt = DateTime.MinValue;
        OverlayVisible = false;
        IsBarcodeMode = false;
        ProductActionsExpanded = false;
        BarcodeChoices.Clear();
        UnknownBarcodeVisible = false;
        UnknownBarcode = "";
        if (!SearchVisible)
            IsDetecting = true;
    }

    [GeneratedRegex("^[0-9a-f]{32}$")]
    private static partial Regex HandoffCode();

    partial void OnPrintCopiesChanged(int value) => OnPropertyChanged(nameof(PrintTotalText));
    partial void OnPrintWithPriceChanged(bool value) => OnPropertyChanged(nameof(BarcodePreviewPrice));
    partial void OnSelectedBarcodeChanged(BarcodeChoice? value) => NotifyBarcodePreviewChanged();

    private void NotifyBarcodePreviewChanged()
    {
        OnPropertyChanged(nameof(BarcodePreviewPrice));
        OnPropertyChanged(nameof(BarcodePreviewSku));
        OnPropertyChanged(nameof(BarcodePreviewNameLines));
    }
}

public sealed record SearchRow(ProductDto Product, ImageUrlBuilder Images)
{
    public string Name => Product.Name;
    public string PriceText => Money.Currency(
        Product.SellingPrice ?? 0,
        Product.PriceCurrency,
        Product.PriceSymbol,
        Product.PriceSymbolPosition,
        Product.PriceDecimalDigits);
    public string StockText => $"{Product.OnHand:0.###} {Product.UnitName}";
    public string? ImageUrl => Images.FromKey(Product.ImageKey, thumb: true) ?? Images.Full(Product.ImageUrl);
    public string FullInfo => $"{Product.Name} | {Product.UnitName} | {Product.Dimension ?? ""}";
}

public sealed partial class BarcodeChoice(BarcodeDto barcode) : ObservableObject
{
    public string Code => barcode.Code;
    public decimal PackQty => barcode.PackQty;
    public string PackText => $"×{barcode.PackQty:0.###}";
}
