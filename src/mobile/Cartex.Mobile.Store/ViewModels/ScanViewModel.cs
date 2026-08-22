using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Querying;
using Cartex.Hub;
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
    private readonly ICategoriesApi _categoriesApi;
    private readonly WarehouseContext _warehouse;
    private readonly MobilePermissions _permissions;
    private readonly CartStore _cart;
    private readonly SupplyCartStore _supplyCart;
    private readonly ImageUrlBuilder _images;
    private readonly MobilePrintDispatcher _printDispatcher;
    private readonly BarcodeLabelSettingsCache _labelSettings;
    private readonly MobileOfflineService _offline;
    private readonly SessionStore _session;
    private readonly StoreSignOut _signOut;

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
    [ObservableProperty] private string _quantityText = "1";
    [ObservableProperty] private bool _canEditProduct;
    [ObservableProperty] private bool _canReceiveStock;
    [ObservableProperty] private int _cartCount;
    [ObservableProperty] private int _supplyCartCount;
    // Qidiruv paneli ochiqmi. Maydonning o'zi doim ko'rinadi — panelni fokus yoki yozilgan
    // matn ochadi, lupa tugmasi emas: ilgari izlash uchun ikki qadam kerak bo'lardi.
    [ObservableProperty] private bool _searchOpen;
    [ObservableProperty] private bool _canSearchProducts;
    [ObservableProperty] private bool _isSearching;
    [ObservableProperty] private bool _isLoadingMoreResults;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private bool _productActionsExpanded;
    [ObservableProperty] private bool _isBarcodeMode;
    [ObservableProperty] private bool _torchOn;
    [ObservableProperty] private bool _isPrintingBarcode;
    [ObservableProperty] private BarcodeChoice? _selectedBarcode;
    [ObservableProperty] private int _printCopies = 1;
    [ObservableProperty] private bool _printWithPrice;
    [ObservableProperty] private bool _printWithSku;
    [ObservableProperty] private bool _canOverridePrintPrice = true;

    public ObservableCollection<SearchRow> SearchResults { get; } = [];
    public ObservableCollection<SearchCategory> SearchCategories { get; } = [];
    public ObservableCollection<BarcodeChoice> BarcodeChoices { get; } = [];
    public bool CanPrintBarcode => _printDispatcher.CanPrintBarcode;
    public bool HasProductActions => CanEditProduct || CanPrintBarcode;
    public string PrintTotalText => $"{Loc.Instance["total"]}: {PrintCopies}";
    public string BarcodePreviewPrice => PrintWithPrice && _product is not null
        ? FormatLabelPrice(_product, _labelSettings.Current)
        : string.Empty;
    public string BarcodePreviewSku => PrintWithSku ? _productSku ?? string.Empty : string.Empty;
    public int BarcodePreviewNameLines => _labelSettings.Current.NameLines;

    private ProductLookupDto? _product;
    private bool _allowsFractional;
    private bool _handled;
    private string? _lastValue;
    private DateTime _lastAt = DateTime.MinValue;
    private CancellationTokenSource? _searchCts;
    private string? _activeSearch;
    private long? _searchCategoryId;
    private bool _searchCategoriesLoaded;
    private int _loadedSearchPage;
    private bool _hasMoreSearchResults;
    private IReadOnlyList<CurrencyDto>? _currencies;
    private string? _activeBarcode;
    private string? _productSku;

    public ScanViewModel(ISessionsApi sessionsApi, IProductsApi productsApi, IRatesApi ratesApi, IBarcodesApi barcodesApi, ICategoriesApi categoriesApi, WarehouseContext warehouse, MobilePermissions permissions, CartStore cart, SupplyCartStore supplyCart, ImageUrlBuilder images, MobilePrintDispatcher printDispatcher, BarcodeLabelSettingsCache labelSettings, MobileOfflineService offline, SessionStore session, StoreSignOut signOut)
    {
        _sessionsApi = sessionsApi;
        _productsApi = productsApi;
        _ratesApi = ratesApi;
        _barcodesApi = barcodesApi;
        _categoriesApi = categoriesApi;
        _warehouse = warehouse;
        _permissions = permissions;
        _cart = cart;
        _supplyCart = supplyCart;
        _images = images;
        _printDispatcher = printDispatcher;
        _labelSettings = labelSettings;
        _offline = offline;
        _session = session;
        _signOut = signOut;
        _cartCount = cart.Count;
        _supplyCartCount = supplyCart.Count;
        WeakReferenceMessenger.Default.Register<ScanViewModel, ProductChangedMessage>(this, static (recipient, message) => _ = recipient.RefreshProductAsync(message.Value));
        CanEditProduct = permissions.Has("products.edit");
        CanReceiveStock = permissions.Has("supplies.create");
        CanSearchProducts = permissions.Has("products.view");
    }

    public void Appear()
    {
        _cart.Changed -= OnCartChanged;
        _cart.Changed += OnCartChanged;
        _supplyCart.Changed -= OnSupplyCartChanged;
        _supplyCart.Changed += OnSupplyCartChanged;
        CartCount = _cart.Count;
        SupplyCartCount = _supplyCart.Count;
    }

    public void Disappear()
    {
        _cart.Changed -= OnCartChanged;
        _supplyCart.Changed -= OnSupplyCartChanged;
        // Qidiruv yopiladi: bo'limga qaytilganda skaner darhol ishlashi kerak, ochiq qolgan
        // panel esa kamerani to'xtatib turardi.
        CloseSearch();
        // Chiroq sahifadan chiqilganda o'chadi: kamera to'xtaganda ham yonib qolsa,
        // telefon bekorga qiziydi va batareya yeyiladi.
        TorchOn = false;
    }

    private void OnCartChanged() => CartCount = _cart.Count;

    public async Task HandleAsync(string value)
    {
        if (_handled || OverlayVisible || UnknownBarcodeVisible || SearchOpen) return;
        KeyboardDismissal.Hide();
        if (value == _lastValue && (DateTime.UtcNow - _lastAt).TotalSeconds < 1.5) return;
        _handled = true;
        _lastValue = value;
        _lastAt = DateTime.UtcNow;
        IsDetecting = false;

        if (value.StartsWith("cartexqr:", StringComparison.OrdinalIgnoreCase))
            await ApproveQrAsync(value["cartexqr:".Length..]);
        else if (QrActions.TryServer(value, out var serverUrl))
            await ConnectServerAsync(serverUrl);
        // HUB QR'i bulut serveri QR'i emas: boshqa prefiks, boshqa protokol va boshqa ishonch.
        else if (HubQr.TryParse(value, out var hubEndpoint))
            await LinkHubAsync(hubEndpoint);
        else if (QrActions.IsWifi(value))
        {
            await QrActions.HandleWifiAsync(value);
            Resume();
        }
        else if (HandoffCode().IsMatch(value))
            await OpenHandoffAsync(value);
        else
            await LookupAsync(value);
    }

    private async Task ApproveQrAsync(string code)
    {
        if (BlockOnlineMutationWhileOffline()) { Resume(); return; }
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

    private async Task ConnectServerAsync(string url)
    {
        var page = Shell.Current.CurrentPage;
        if (page is null)
        {
            Resume();
            return;
        }
        if (QrActions.IsSameServer(url, _session.ServerUrl))
        {
            Ui.Toast(Loc.Instance["server_already_connected"]);
            Resume();
            return;
        }
        // Lease hali o'qilmagan bo'lsa navbat sanog'i nol chiqadi va yuborilmagan
        // amallar qo'riqchisi ishlamay qoladi.
        await _offline.StartAsync();
        if (await _offline.PendingCountAsync() + await _offline.ErrorCountAsync() > 0)
        {
            await page.DisplayAlertAsync(Loc.Instance["server"], Loc.Instance["server_switch_pending"], Loc.Instance["ok"]);
            Resume();
            return;
        }
        if (!await page.DisplayAlertAsync(
                Loc.Instance["server"], string.Format(Loc.Instance["server_connect_confirm"], url),
                Loc.Instance["yes"], Loc.Instance["no"]))
        {
            Resume();
            return;
        }
        if (!await QrActions.ProbeServerAsync(url))
        {
            await page.DisplayAlertAsync(Loc.Instance["server"], Loc.Instance["server_unreachable"], Loc.Instance["ok"]);
            Resume();
            return;
        }
        // Eski do'kon nusxasi yangi serverda ishlatilmasligi kerak; navbat bo'sh ekani
        // yuqorida tekshirilgani uchun bu yerda hech narsa yo'qolmaydi.
        await _signOut.RunAsync();
        await _offline.DeactivateLocalAsync();
        _session.ServerUrl = url;
        await Shell.Current.GoToAsync("//login");
        Ui.Toast(Loc.Instance["server_switch_login"]);
    }

    // HUB-11: e'lonni bloklaydigan tarmoqda ulanishning qo'l bilan boriladigan yo'li.
    private async Task LinkHubAsync(Uri endpoint)
    {
        var page = Shell.Current.CurrentPage;
        if (page is null)
        {
            Resume();
            return;
        }
        await _offline.StartAsync();
        if (_offline.IsEnabled || _offline.IsSatellite)
        {
            Ui.Toast(Loc.Instance["hub_link_not_needed"]);
            Resume();
            return;
        }
        if (!await page.DisplayAlertAsync(
                Loc.Instance["hub_link_title"],
                string.Format(Loc.Instance["hub_link_confirm"], endpoint.Host),
                Loc.Instance["yes"], Loc.Instance["no"]))
        {
            Resume();
            return;
        }

        Status = Loc.Instance["hub_searching"];
        if (!await _offline.LinkToHubAsync(endpoint))
        {
            await FlashAsync(Loc.Instance["hub_link_failed"]);
            return;
        }
        Ui.Toast(Loc.Instance["hub_linked"]);
        Resume();
    }

    private async Task OpenHandoffAsync(string code)
    {
        if (BlockOnlineMutationWhileOffline()) { Resume(); return; }
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
        await _offline.StartAsync();
        if (_offline.ShouldUseOffline)
        {
            await LookupOfflineAsync(barcode);
            return;
        }
        try
        {
            var product = await _productsApi.GetByBarcodeAsync(barcode, _warehouse.WarehouseId!.Value, forSale: false);
            ProductActionsExpanded = false;
            _product = product;
            _activeBarcode = barcode;
            _productSku = null;
            ConfigureQuantity(product, product.PackQty > 0 ? product.PackQty : 1);
            ProductName = product.ProductName;
            PriceText = FormatPrice(product);
            StockText = StockTextFor(product.OnHand, product.UnitName, product.VariantId);
            OverlayVisible = true;
            Ui.Vibrate();

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
            _offline.MarkServerUnavailable();
            if (_offline.IsEnabled)
                await LookupOfflineAsync(barcode);
            else
                await FlashAsync(Loc.Instance["err_no_connection"]);
        }
    }

    private async Task LookupOfflineAsync(string barcode)
    {
        var product = await _offline.FindProductAsync(barcode);
        if (product is null)
        {
            if (await TryOpenLinkAsync(barcode)) return;
            Ui.Vibrate(2);
            await FlashAsync(Loc.Instance["offline_product_not_cached"]);
            return;
        }
        ProductActionsExpanded = false;
        _product = product;
        _activeBarcode = barcode;
        _productSku = null;
        ConfigureQuantity(product, product.PackQty > 0 ? product.PackQty : 1);
        ProductName = product.ProductName;
        PriceText = FormatPrice(product);
        StockText = StockTextFor(product.OnHand, product.UnitName, product.VariantId);
        ImageUrl = null;
        OverlayVisible = true;
        Ui.Vibrate();
    }

    private async Task HandleUnknownBarcodeAsync(string barcode)
    {
        if (await TryOpenLinkAsync(barcode)) return;
        Ui.Vibrate(2);
        UnknownBarcode = barcode;
        UnknownBarcodeVisible = true;
    }

    private async Task<bool> TryOpenLinkAsync(string barcode)
    {
        if (!QrActions.IsHttpUrl(barcode)) return false;
        await QrActions.OpenLinkAsync(barcode);
        Resume();
        return true;
    }

    [RelayCommand]
    private async Task AttachUnknownBarcodeAsync()
    {
        if (BlockOnlineMutationWhileOffline()) { Resume(); return; }
        if (string.IsNullOrEmpty(UnknownBarcode)) return;

        var barcode = UnknownBarcode;
        UnknownBarcodeVisible = false;
        await Shell.Current.GoToAsync($"barcode_attach?barcode={Uri.EscapeDataString(barcode)}");
        Resume();
    }

    [RelayCommand]
    private async Task CreateProductFromUnknownBarcodeAsync()
    {
        if (BlockOnlineMutationWhileOffline()) { Resume(); return; }
        if (string.IsNullOrEmpty(UnknownBarcode)) return;

        var barcode = UnknownBarcode;
        UnknownBarcodeVisible = false;
        await Shell.Current.GoToAsync($"product/edit?id=0&barcode={Uri.EscapeDataString(barcode)}");
        Resume();
    }

    [RelayCommand]
    private void CloseUnknownBarcode() => Resume();

    [RelayCommand]
    private void Increase() => Quantity += 1m;

    [RelayCommand]
    private void Decrease() => Quantity = Math.Max(1m, Quantity - 1m);

    [RelayCommand]
    private void SetQuantityFromText() => TryCommitQuantity(showError: true);

    [RelayCommand]
    private void AddToCart()
    {
        if (_product is null || !TryCommitQuantity(showError: true)) return;
        _cart.Add(_product, Quantity);
        Ui.Toast(Loc.Instance["added_to_cart"]);
        CloseOverlay();
    }

    [RelayCommand]
    private async Task EditProductAsync()
    {
        if (BlockOnlineMutationWhileOffline()) return;
        var variantId = _product?.VariantId;
        if (variantId is null) return;
        CloseOverlay();
        await Shell.Current.GoToAsync($"product/edit?id={variantId}");
    }

    [RelayCommand]
    private async Task OpenBarcodePrintAsync()
    {
        if (BlockOnlineMutationWhileOffline()) return;
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
            PrintWithSku = settings.ShowSku;
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
                PrintWithPrice,
                PrintWithSku);
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
    private async Task ReceiveStockAsync()
    {
        if (_product is null) return;
        var product = _product;
        var existing = _supplyCart.Lines.FirstOrDefault(l => l.VariantId == product.VariantId);
        var quantity = (existing?.Quantity ?? 0) + Quantity;
        _supplyCart.Add(product, existing?.PurchasePrice ?? 0);
        _supplyCart.SetQuantity(product.VariantId, quantity);
        Ui.Toast($"{Loc.Instance["receive_stock"]} ✓");
        CloseOverlay();

        // Tannarx qo'lda kiritiladi, lekin oxirgi xarid narxi bilan oldindan to'ldiriladi —
        // aks holda kassir har safar noldan yozadi va nol qolib ketish xavfi tug'iladi.
        if (existing is not null || _offline.ShouldUseOffline || _warehouse.WarehouseId is not { } warehouseId) return;
        try
        {
            var info = await Task.Run(() => _productsApi.GetVariantPriceInfoAsync(product.VariantId, warehouseId));
            if (info.LastPurchasePrice is > 0 and { } last)
                _supplyCart.SetPurchasePrice(product.VariantId, last);
            if (product.SellingPrice <= 0 && info.SellingPrice is > 0 and { } selling)
                _supplyCart.SetSellingPrice(product.VariantId, selling);
        }
        catch
        {
            // Narx qo'lda kiritilaveradi; oldindan to'ldirish qulaylik, shart emas.
        }
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
    private void ToggleTorch() => TorchOn = !TorchOn;

    // Panel ochilganda kamera to'xtaydi: yozayotganda tasodifan tushgan shtrixkod
    // foydalanuvchini boshqa ekranga olib ketmasin.
    partial void OnSearchOpenChanged(bool value)
    {
        if (value)
        {
            IsDetecting = false;
            _ = EnsureSearchCategoriesAsync();
        }
        else if (!OverlayVisible)
            IsDetecting = true;
    }

    public void SetSearchFocused(bool focused)
    {
        if (focused)
        {
            if (CanSearchProducts) SearchOpen = true;
            return;
        }

        // Bo'sh maydondan chiqilsa panel yopiladi — aks holda kamera bekorga to'xtab turardi.
        if (SearchText.Length == 0 && _searchCategoryId is null)
            CloseSearch();
    }

    private async Task EnsureSearchCategoriesAsync()
    {
        if (_searchCategoriesLoaded) return;
        try
        {
            var categories = await _categoriesApi.GetAllAsync();
            SearchCategories.Add(new SearchCategory(0, Loc.Instance["filter_all"]) { IsSelected = true });
            foreach (var category in categories)
                SearchCategories.Add(new SearchCategory(category.Id, category.Name));
            _searchCategoriesLoaded = true;
        }
        catch { }
    }

    [RelayCommand]
    private void SelectSearchCategory(SearchCategory category)
    {
        if (category.IsSelected) return;
        foreach (var item in SearchCategories)
            item.IsSelected = item.Id == category.Id;
        _searchCategoryId = category.Id == 0 ? null : category.Id;
        RestartSearch();
    }

    [RelayCommand]
    private void CloseSearch()
    {
        KeyboardDismissal.Hide();
        Debounce.Cancel(ref _searchCts);
        IsSearching = false;
        _searchCategoryId = null;
        foreach (var category in SearchCategories)
            category.IsSelected = category.Id == 0;
        SearchResults.Clear();
        SearchText = "";
        SearchOpen = false;
    }

    [RelayCommand]
    private async Task CreateProductFromSearchAsync()
    {
        if (BlockOnlineMutationWhileOffline()) return;
        if (!CanEditProduct) return;

        var barcode = SearchText.Trim();
        CloseSearch();
        var route = string.IsNullOrWhiteSpace(barcode)
            ? "product/edit?id=0"
            : $"product/edit?id=0&barcode={Uri.EscapeDataString(barcode)}";
        await Shell.Current.GoToAsync(route);
        Resume();
    }

    partial void OnSearchTextChanged(string value) => RestartSearch();

    private void RestartSearch()
    {
        Debounce.Cancel(ref _searchCts);
        SearchResults.Clear();
        _activeSearch = null;
        _loadedSearchPage = 0;
        _hasMoreSearchResults = false;
        IsLoadingMoreResults = false;

        var text = SearchText.Trim();
        if (text.Length < 2 && _searchCategoryId is null)
        {
            IsSearching = false;
            return;
        }

        SearchOpen = true;

        var cts = _searchCts = new CancellationTokenSource();
        _activeSearch = text.Length < 2 ? null : text;
        IsSearching = true;
        _ = SearchAsync(_activeSearch, cts, 1);
    }

    private async Task SearchAsync(string? text, CancellationTokenSource cts, int page)
    {
        try
        {
            await Task.Delay(200, cts.Token);
            if (_offline.ShouldUseOffline)
            {
                if (text is null) return;
                var cached = page == 1
                    ? await _offline.SearchProductsAsync(text, SearchPageSize)
                    : [];
                if (cts.IsCancellationRequested) return;
                _loadedSearchPage = page;
                _hasMoreSearchResults = false;
                foreach (var product in cached)
                    SearchResults.Add(new SearchRow(product, _images));
                return;
            }
            var response = await _productsApi.QueryAsync(QueryRequest.Create()
                .Page(page, SearchPageSize).Search(text).With("categoryId", _searchCategoryId).Build());
            if (cts.IsCancellationRequested) return;

            var products = (response.Content ?? []).ToList();
            _loadedSearchPage = page;
            _hasMoreSearchResults = products.Count == SearchPageSize;
            foreach (var p in products.Where(p => p.IsEnabled))
                SearchResults.Add(new SearchRow(p, _images));
        }
        catch (OperationCanceledException) { }
        catch
        {
            _offline.MarkServerUnavailable();
            if (text is not null && page == 1 && _offline.IsEnabled && !cts.IsCancellationRequested)
            {
                foreach (var product in await _offline.SearchProductsAsync(text, SearchPageSize))
                    SearchResults.Add(new SearchRow(product, _images));
                _hasMoreSearchResults = false;
            }
        }
        finally
        {
            if (_searchCts == cts)
                IsSearching = false;
        }
    }

    [RelayCommand]
    private async Task LoadMoreResultsAsync()
    {
        if (IsSearching || IsLoadingMoreResults || !_hasMoreSearchResults || _searchCts is null
            || (_activeSearch is null && _searchCategoryId is null))
            return;

        var cts = _searchCts;
        var page = _loadedSearchPage + 1;
        IsLoadingMoreResults = true;
        try
        {
            var response = await _productsApi.QueryAsync(QueryRequest.Create()
                .Page(page, SearchPageSize).Search(_activeSearch).With("categoryId", _searchCategoryId).Build());
            if (cts.IsCancellationRequested || _searchCts != cts)
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
        KeyboardDismissal.Hide();
        var p = row.Product;
        ProductActionsExpanded = false;
        try
        {
            _product = _offline.ShouldUseOffline
                ? await _offline.FindProductByVariantAsync(p.DefaultVariantId, 1)
                  ?? throw new InvalidOperationException(Loc.Instance["offline_product_not_cached"])
                : await BuildLookupAsync(p, 1);
        }
        catch (InvalidOperationException ex)
        {
            Ui.Toast(ex.Message);
            return;
        }
        catch (Exception ex)
        {
            Ui.Toast(ex is Refit.ApiException api ? ApiErrors.Describe(api) : Loc.Instance["err_no_connection"]);
            return;
        }
        ConfigureQuantity(_product, 1);
        _activeBarcode = p.Barcodes.FirstOrDefault();
        _productSku = p.Code;
        ProductName = p.Name;
        PriceText = FormatPrice(_product);
        StockText = StockTextFor(p.OnHand, p.UnitName, p.DefaultVariantId);
        OverlayVisible = true;
        IsDetecting = false;
        ImageUrl = _images.FromKey(p.ImageKey, thumb: false);
        CloseSearch();
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
            if (_offline.ShouldUseOffline)
            {
                var cached = await _offline.FindProductByVariantAsync(variantId, _product.PackQty);
                if (cached is null) return;
                _product = cached;
                ConfigureQuantity(_product, Quantity);
                ProductName = _product.ProductName;
                PriceText = FormatPrice(_product);
                StockText = StockTextFor(_product.OnHand, _product.UnitName, _product.VariantId);
                ImageUrl = null;
                return;
            }
            var updated = (await _productsApi.GetAllAsync(variantId: variantId)).FirstOrDefault();
            if (updated is null)
                return;

            _product = await BuildLookupAsync(updated, _product.PackQty);
            ConfigureQuantity(_product, Quantity);
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
            originalPrice,
            priceCurrency,
            baseCurrency,
            rate,
            product.AllowsFractional);
    }

    partial void OnQuantityChanged(decimal value) => QuantityText = QuantityInput.Format(value);

    private void ConfigureQuantity(ProductLookupDto product, decimal initialQuantity)
    {
        _allowsFractional = product.AllowsFractional;
        Quantity = Math.Max(QuantityInput.NormalizeStep(_allowsFractional), initialQuantity);
    }

    private bool TryCommitQuantity(bool showError)
    {
        if (QuantityInput.TryParse(QuantityText, _allowsFractional, out var quantity, out var error))
        {
            Quantity = quantity;
            return true;
        }

        QuantityText = QuantityInput.Format(Quantity);
        if (showError)
            Ui.Toast(Loc.Instance[error switch
            {
                QuantityInputError.MustBePositive => "quantity_positive_required",
                QuantityInputError.FractionNotAllowed => "quantity_integer_required",
                _ => "quantity_invalid"
            }]);
        return false;
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
        var number = CurrencyCatalog.FormatLabelNumber(amount, metadata.DecimalDigits);
        var position = settings.CurrencyDisplay == "code" ? "Suffix" : metadata.SymbolPosition;
        return position == "Prefix" ? $"{token}{number}" : $"{number} {token}";
    }

    private void Resume()
    {
        Status = Loc.Instance["scan_hint_store"];
        _handled = false;
        _lastAt = DateTime.UtcNow;
        OverlayVisible = false;
        IsBarcodeMode = false;
        ProductActionsExpanded = false;
        BarcodeChoices.Clear();
        UnknownBarcodeVisible = false;
        UnknownBarcode = "";
        if (!SearchOpen)
            IsDetecting = true;
    }

    [GeneratedRegex("^[0-9a-f]{32}$", RegexOptions.None, matchTimeoutMilliseconds: 200)]
    private static partial Regex HandoffCode();

    partial void OnPrintCopiesChanged(int value) => OnPropertyChanged(nameof(PrintTotalText));
    partial void OnPrintWithPriceChanged(bool value) => OnPropertyChanged(nameof(BarcodePreviewPrice));
    partial void OnPrintWithSkuChanged(bool value) => OnPropertyChanged(nameof(BarcodePreviewSku));
    partial void OnSelectedBarcodeChanged(BarcodeChoice? value) => NotifyBarcodePreviewChanged();

    private void NotifyBarcodePreviewChanged()
    {
        OnPropertyChanged(nameof(BarcodePreviewPrice));
        OnPropertyChanged(nameof(BarcodePreviewSku));
        OnPropertyChanged(nameof(BarcodePreviewNameLines));
    }

    private bool BlockOnlineMutationWhileOffline()
    {
        if (!_offline.ShouldUseOffline) return false;
        Ui.Toast(Loc.Instance["offline_mutation_blocked"]);
        return true;
    }
}

public sealed partial class SearchCategory(long id, string name) : ObservableObject
{
    public long Id { get; } = id;
    public string Name { get; } = name;
    [ObservableProperty] private bool _isSelected;
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
    public string? ImageUrl { get; } = Images.FromKey(Product.ImageKey, thumb: true) ?? Images.Full(Product.ImageUrl);
    public string FullInfo => $"{Product.Name} | {Product.UnitName} | {Product.Dimension ?? ""}";
}

public sealed class BarcodeChoice(BarcodeDto barcode) : ObservableObject
{
    public string Code => barcode.Code;
    public decimal PackQty => barcode.PackQty;
    public string PackText => $"×{barcode.PackQty:0.###}";
}
