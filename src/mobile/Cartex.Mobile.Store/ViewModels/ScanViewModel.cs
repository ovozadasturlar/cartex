using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Querying;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;
using Cartex.Shared.Models.Auth;
using Cartex.Shared.Models.Products;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace Cartex.Mobile.Store.ViewModels;

public partial class ScanViewModel : ObservableObject
{
    private const int SearchPageSize = 30;

    private readonly ISessionsApi _sessionsApi;
    private readonly IProductsApi _productsApi;
    private readonly WarehouseContext _warehouse;
    private readonly MobilePermissions _permissions;
    private readonly CartStore _cart;
    private readonly SupplyCartStore _supplyCart;
    private readonly ImageUrlBuilder _images;

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

    public ObservableCollection<SearchRow> SearchResults { get; } = [];

    private ProductLookupDto? _product;
    private decimal _step = 1;
    private bool _handled;
    private string? _lastValue;
    private DateTime _lastAt = DateTime.MinValue;
    private CancellationTokenSource? _searchCts;
    private string? _activeSearch;
    private int _loadedSearchPage;
    private bool _hasMoreSearchResults;

    public ScanViewModel(ISessionsApi sessionsApi, IProductsApi productsApi, WarehouseContext warehouse, MobilePermissions permissions, CartStore cart, SupplyCartStore supplyCart, ImageUrlBuilder images)
    {
        _sessionsApi = sessionsApi;
        _productsApi = productsApi;
        _warehouse = warehouse;
        _permissions = permissions;
        _cart = cart;
        _supplyCart = supplyCart;
        _images = images;
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
        // Debounce: ignore same barcode within 1.5s (not 2s, so closing overlay quickly then scanning again works)
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
            _product = product;
            _step = product.PackQty > 0 ? product.PackQty : 1;
            Quantity = _step;
            ProductName = product.ProductName;
            PriceText = $"{product.SellingPrice:N0} UZS";
            StockText = StockTextFor(product.OnHand, product.UnitName, product.VariantId);
            OverlayVisible = true;
            
            ImageUrl = _images.FromKey(product.ImageKey, thumb: false);
        }
        catch (Refit.ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Unknown barcode — ask user what to do
            await HandleUnknownBarcodeAsync(barcode);
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
    private Task EditProduct() => Shell.Current.GoToAsync($"product/edit?id={_product?.VariantId}");

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
    private void PickResult(SearchRow row)
    {
        var p = row.Product;
        _product = new ProductLookupDto(p.DefaultVariantId, p.Name, p.UnitName, 1, p.SellingPrice ?? 0, p.OnHand, p.Dimension ?? "", p.ImageKey);
        _step = 1;
        Quantity = 1;
        ProductName = p.Name;
        PriceText = $"{p.SellingPrice ?? 0:N0} UZS";
        StockText = StockTextFor(p.OnHand, p.UnitName, p.DefaultVariantId);
        OverlayVisible = true;
        IsDetecting = false;
        ImageUrl = _images.FromKey(p.ImageKey, thumb: false);
        
        SearchVisible = false;
        SearchText = "";
        SearchResults.Clear();
        // Progressive loading removed – images are now set directly in LookupAsync / PickResult
    }

    // Long‑press on a search row to view full product details in a modal
    [RelayCommand]
    private void RowLongPress(SearchRow row)
    {
        // Navigate to a modal page showing detailed product information.
        // The page "product/detail" should be implemented to display all fields.
        if (row?.Product?.DefaultVariantId != null)
        {
            var url = $"product/detail?variantId={row.Product.DefaultVariantId}";
            // Using modal navigation (true) to present as a modal dialog.
            Shell.Current.GoToAsync(url, true);
        }
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

            _product = new ProductLookupDto(variantId, updated.Name, updated.UnitName, _product.PackQty,
                updated.SellingPrice ?? _product.SellingPrice, updated.OnHand, updated.Dimension ?? _product.Dimension,
                updated.ImageKey);
            ProductName = _product.ProductName;
            PriceText = $"{_product.SellingPrice:N0} UZS";
            StockText = StockTextFor(_product.OnHand, _product.UnitName, _product.VariantId);
            ImageUrl = _images.FromKey(_product.ImageKey, thumb: false);
        }
        catch { }
    }

    private void Resume()
    {
        Status = Loc.Instance["scan_hint_store"];
        _handled = false;
        _lastValue = null;
        _lastAt = DateTime.MinValue;
        OverlayVisible = false;
        UnknownBarcodeVisible = false;
        UnknownBarcode = "";
        if (!SearchVisible)
            IsDetecting = true;
    }

    [GeneratedRegex("^[0-9a-f]{32}$")]
    private static partial Regex HandoffCode();
}

public sealed record SearchRow(ProductDto Product, ImageUrlBuilder Images)
{
    public string Name => Product.Name;
    public string PriceText => $"{Product.SellingPrice ?? 0:N0} UZS";
    public string StockText => $"{Product.OnHand:0.###} {Product.UnitName}";
    public string? ImageUrl => Images.FromKey(Product.ImageKey, thumb: true) ?? Images.Full(Product.ImageUrl);
    // Additional info that helps differentiate products with similar names.
    public string FullInfo => $"{Product.Name} | {Product.UnitName} | {Product.Dimension ?? ""}";
}
