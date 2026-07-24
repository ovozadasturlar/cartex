using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.Mobile.Core;
using Cartex.Shared.Models.Barcodes;
using Cartex.Shared.Models.Products;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.Mobile.Store.ViewModels;

public partial class BarcodeAttachViewModel : ObservableObject, IQueryAttributable
{
    private readonly IProductsApi _products;
    private readonly IBarcodesApi _barcodes;
    private readonly ImageUrlBuilder _images;

    private string _barcode = "";
    private CancellationTokenSource? _cts;

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _error;

    public ObservableCollection<AttachRow> Results { get; } = [];

    public BarcodeAttachViewModel(IProductsApi products, IBarcodesApi barcodes, ImageUrlBuilder images)
    {
        _products = products;
        _barcodes = barcodes;
        _images = images;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("barcode", out var b))
            _barcode = Convert.ToString(b) ?? "";
    }

    partial void OnSearchTextChanged(string value)
    {
        _cts?.Cancel();
        var cts = _cts = new CancellationTokenSource();
        _ = SearchAsync(value.Trim(), cts.Token);
    }

    private async Task SearchAsync(string text, CancellationToken ct)
    {
        if (text.Length < 2)
        {
            Results.Clear();
            return;
        }
        try
        {
            await Task.Delay(300, ct);
            var list = await _products.GetAllAsync(search: text);
            if (ct.IsCancellationRequested) return;
            Results.Clear();
            foreach (var p in list.Where(p => p.IsEnabled).Take(40))
                Results.Add(new AttachRow(p, _images));
        }
        catch (OperationCanceledException) { }
        catch { }
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
}

public sealed record AttachRow(ProductDto Product, ImageUrlBuilder Images)
{
    public string Name => Product.Name;
    public string PriceText => $"{Product.SellingPrice ?? 0:N0} UZS";
    public string StockText => $"{Product.OnHand:0.###} {Product.UnitName}";
    public string? ImageUrl => Images.Full(Product.ImageUrl);
    public string? CategoryName => Product.CategoryName;
}
