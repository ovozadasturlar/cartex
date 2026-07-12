using System.Text.RegularExpressions;
using Cartex.ApiClient.Api;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;
using Cartex.Shared.Models.Auth;
using Cartex.Shared.Models.Products;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.Mobile.Store.ViewModels;

public partial class ScanViewModel : ObservableObject
{
    private readonly ISessionsApi _sessionsApi;
    private readonly IProductsApi _productsApi;
    private readonly WarehouseContext _warehouse;
    private readonly MobilePermissions _permissions;
    private readonly CartStore _cart;

    [ObservableProperty] private bool _isDetecting = true;
    [ObservableProperty] private string? _status = Loc.Instance["scan_hint_store"];
    [ObservableProperty] private bool _overlayVisible;
    [ObservableProperty] private string _productName = "";
    [ObservableProperty] private string _priceText = "";
    [ObservableProperty] private string _stockText = "";
    [ObservableProperty] private decimal _quantity;
    [ObservableProperty] private int _cartCount;
    [ObservableProperty] private bool _searchVisible;
    [ObservableProperty] private string _searchText = "";

    public System.Collections.ObjectModel.ObservableCollection<SearchRow> SearchResults { get; } = [];

    private ProductLookupDto? _product;
    private decimal _step = 1;
    private bool _handled;
    private string? _lastValue;
    private DateTime _lastAt;
    private CancellationTokenSource? _searchCts;

    public ScanViewModel(ISessionsApi sessionsApi, IProductsApi productsApi, WarehouseContext warehouse, MobilePermissions permissions, CartStore cart)
    {
        _sessionsApi = sessionsApi;
        _productsApi = productsApi;
        _warehouse = warehouse;
        _permissions = permissions;
        _cart = cart;
        _cartCount = cart.Count;
        cart.Changed += () => CartCount = _cart.Count;
    }

    public async Task HandleAsync(string value)
    {
        if (_handled || OverlayVisible) return;
        if (value == _lastValue && (DateTime.UtcNow - _lastAt).TotalSeconds < 2) return;
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
        var confirmed = page is not null && await page.DisplayAlert(
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
        if (_permissions.Has("sales.create"))
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
            var product = await _productsApi.GetByBarcodeAsync(barcode, _warehouse.WarehouseId!.Value);
            _product = product;
            _step = product.PackQty > 0 ? product.PackQty : 1;
            Quantity = _step;
            ProductName = product.ProductName;
            PriceText = $"{product.SellingPrice:N0} UZS";
            StockText = $"{Loc.Instance["stock_label"]}{product.OnHand:0.###} {product.UnitName}";
            OverlayVisible = true;
        }
        catch (Refit.ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            await FlashAsync(Loc.Instance["product_not_found"]);
        }
        catch
        {
            await FlashAsync(Loc.Instance["err_no_connection"]);
        }
    }

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
        IsDetecting = !SearchVisible && !OverlayVisible;
        if (!SearchVisible)
        {
            SearchText = "";
            SearchResults.Clear();
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        _searchCts?.Cancel();
        var cts = _searchCts = new CancellationTokenSource();
        _ = SearchAsync(value.Trim(), cts.Token);
    }

    private async Task SearchAsync(string text, CancellationToken ct)
    {
        try
        {
            await Task.Delay(350, ct);
            if (text.Length < 2)
            {
                SearchResults.Clear();
                return;
            }
            var products = await _productsApi.GetAllAsync(search: text);
            if (ct.IsCancellationRequested) return;
            SearchResults.Clear();
            foreach (var p in products.Take(30))
                SearchResults.Add(new SearchRow(p));
        }
        catch (OperationCanceledException) { }
        catch { }
    }

    [RelayCommand]
    private void PickResult(SearchRow row)
    {
        var p = row.Product;
        _product = new ProductLookupDto(p.DefaultVariantId, p.Name, p.UnitName, 1, p.SellingPrice ?? 0, p.OnHand, p.Dimension ?? "");
        _step = 1;
        Quantity = 1;
        ProductName = p.Name;
        PriceText = $"{p.SellingPrice ?? 0:N0} UZS";
        StockText = $"{Loc.Instance["stock_label"]}{p.OnHand:0.###} {p.UnitName}";
        SearchVisible = false;
        OverlayVisible = true;
    }

    private async Task FlashAsync(string message)
    {
        Status = message;
        await Task.Delay(1800);
        Resume();
    }

    private void Resume()
    {
        Status = Loc.Instance["scan_hint_store"];
        _handled = false;
        IsDetecting = true;
    }

    [GeneratedRegex("^[0-9a-f]{32}$")]
    private static partial Regex HandoffCode();
}

public sealed record SearchRow(ProductDto Product)
{
    public string Name => Product.Name;
    public string PriceText => $"{Product.SellingPrice ?? 0:N0} UZS";
    public string StockText => $"{Product.OnHand:0.###} {Product.UnitName}";
}
