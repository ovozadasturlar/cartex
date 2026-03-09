using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Products;

namespace Cartex.Desktop.ViewModels;

public partial class ProductsViewModel : ViewModelBase
{
    private readonly IProductsApi _productsApi;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private ProductDto? _selectedProduct;

    public ObservableCollection<ProductDto> Products { get; } = [];

    public ProductsViewModel(IProductsApi productsApi)
    {
        _productsApi = productsApi;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText;
            var products = await _productsApi.GetAllAsync(search: search);
            Products.Clear();
            foreach (var p in products)
                Products.Add(p);
        }
        catch { }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    partial void OnSearchTextChanged(string value)
    {
        _ = LoadAsync();
    }
}
