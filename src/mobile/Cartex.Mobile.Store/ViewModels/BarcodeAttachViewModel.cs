using System.Collections.ObjectModel;
using System.Text.Json;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Querying;
using Cartex.Mobile.Core;
using Cartex.Shared.Models.Barcodes;
using Cartex.Shared.Models.Products;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.Mobile.Store.ViewModels;

public partial class BarcodeAttachViewModel : ObservableObject, IQueryAttributable
{
    private const int PageSize = 30;

    private readonly IProductsApi _products;
    private readonly IBarcodesApi _barcodes;
    private readonly ImageUrlBuilder _images;
    private readonly MobilePermissions _permissions;

    private string _barcode = "";
    private CancellationTokenSource? _cts;
    private string? _activeSearch;
    private int _loadedPage;
    private bool _hasMoreResults;

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isSearching;
    [ObservableProperty] private bool _isLoadingMore;
    [ObservableProperty] private bool _isDetailsOpen;
    [ObservableProperty] private AttachRow? _selectedRow;
    [ObservableProperty] private bool _canEditProduct;
    [ObservableProperty] private string? _error;

    public ObservableCollection<AttachRow> Results { get; } = [];

    public BarcodeAttachViewModel(IProductsApi products, IBarcodesApi barcodes, ImageUrlBuilder images, MobilePermissions permissions)
    {
        _products = products;
        _barcodes = barcodes;
        _images = images;
        _permissions = permissions;
        CanEditProduct = permissions.Has("products.manage");
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("barcode", out var b))
            _barcode = Convert.ToString(b) ?? "";
    }

    partial void OnSearchTextChanged(string value)
    {
        _cts?.Cancel();
        Results.Clear();
        Error = null;
        _activeSearch = null;
        _loadedPage = 0;
        _hasMoreResults = false;

        var text = value.Trim();
        if (text.Length < 2)
        {
            IsSearching = false;
            return;
        }

        var cts = _cts = new CancellationTokenSource();
        _activeSearch = text;
        IsSearching = true;
        _ = SearchAsync(text, cts, 1);
    }

    private async Task SearchAsync(string text, CancellationTokenSource cts, int page)
    {
        try
        {
            await Task.Delay(200, cts.Token);
            var response = await _products.QueryAsync(QueryRequest.Create().Page(page, PageSize).Search(text).Build());
            if (cts.IsCancellationRequested) return;

            var products = (response.Content ?? []).ToList();
            _loadedPage = page;
            _hasMoreResults = products.Count == PageSize;
            foreach (var p in products.Where(p => p.IsEnabled))
                Results.Add(new AttachRow(p, _images));
        }
        catch (OperationCanceledException) { }
        catch
        {
            if (!cts.IsCancellationRequested)
                Error = Loc.Instance["err_no_connection"];
        }
        finally
        {
            if (_cts == cts)
                IsSearching = false;
        }
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (IsSearching || IsLoadingMore || !_hasMoreResults || string.IsNullOrWhiteSpace(_activeSearch) || _cts is null)
            return;

        var cts = _cts;
        var page = _loadedPage + 1;
        IsLoadingMore = true;
        try
        {
            var response = await _products.QueryAsync(QueryRequest.Create().Page(page, PageSize).Search(_activeSearch).Build());
            if (cts.IsCancellationRequested || _cts != cts || _activeSearch is null)
                return;

            var products = (response.Content ?? []).ToList();
            _loadedPage = page;
            _hasMoreResults = products.Count == PageSize;
            foreach (var p in products.Where(p => p.IsEnabled))
                Results.Add(new AttachRow(p, _images));
        }
        catch
        {
            if (!cts.IsCancellationRequested)
                Error = Loc.Instance["err_no_connection"];
        }
        finally
        {
            if (_cts == cts)
                IsLoadingMore = false;
        }
    }

    [RelayCommand]
    private async Task AttachAsync(AttachRow row)
    {
        if (IsBusy) return;
        IsBusy = true;
        Error = null;
        try
        {
            await _barcodes.CreateAsync(new CreateBarcodeRequest(row.Product.DefaultVariantId, _barcode, 1));
            Ui.Toast(Loc.Instance["saved_successfully"]);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task AddNewAsync() =>
        Shell.Current.GoToAsync($"product/edit?id=0&barcode={Uri.EscapeDataString(_barcode)}");

    [RelayCommand]
    private void OpenDetails(AttachRow row)
    {
        SelectedRow = row;
        IsDetailsOpen = true;
    }

    [RelayCommand]
    private void CloseDetails() => IsDetailsOpen = false;

    [RelayCommand]
    private Task OpenEditAsync()
    {
        if (SelectedRow is null || !CanEditProduct)
            return Task.CompletedTask;

        IsDetailsOpen = false;
        return Shell.Current.GoToAsync($"product/edit?id={SelectedRow.Product.DefaultVariantId}");
    }
}

public sealed record AttachRow(ProductDto Product, ImageUrlBuilder Images)
{
    public string Name => Product.Name;
    public string PriceText => $"{Product.SellingPrice ?? 0:N0} UZS";
    public string StockText => $"{Product.OnHand:0.###} {Product.UnitName}";
    public string? ImageUrl => ImageUrlFor(thumb: true);
    public string? FullImageUrl => ImageUrlFor(thumb: false);
    public string? CategoryName => Product.CategoryName;
    public string? Code => Product.Code;
    public string? AttributesText => FormatAttributes(Product.Attributes);
    public string BarcodesText => string.Join(", ", Product.Barcodes);
    public bool HasBarcodes => Product.Barcodes.Count > 0;

    private string? ImageUrlFor(bool thumb) =>
        Images.FromKey(Product.ImageKey, thumb) ?? Images.Full(Product.ImageUrl);

    private static string? FormatAttributes(string? attributes)
    {
        if (string.IsNullOrWhiteSpace(attributes))
            return null;

        try
        {
            using var document = JsonDocument.Parse(attributes);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return attributes;

            return string.Join(" · ", document.RootElement.EnumerateObject()
                .Select(attribute => $"{attribute.Name}: {attribute.Value}"));
        }
        catch (JsonException)
        {
            return attributes;
        }
    }
}
