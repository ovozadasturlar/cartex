using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Warehouses;
using Cartex.Shared.Models.Stocks;

namespace Cartex.Desktop.ViewModels;

public partial class WarehouseViewModel : ViewModelBase
{
    private readonly IWarehousesApi _warehousesApi;
    private readonly IStocksApi _stocksApi;

    [ObservableProperty]
    private WarehouseDto? _selectedWarehouse;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public ObservableCollection<WarehouseDto> Warehouses { get; } = [];
    public ObservableCollection<StockDto> Stocks { get; } = [];

    public WarehouseViewModel(IWarehousesApi warehousesApi, IStocksApi stocksApi)
    {
        _warehousesApi = warehousesApi;
        _stocksApi = stocksApi;
    }

    [RelayCommand]
    private async Task LoadWarehousesAsync()
    {
        try
        {
            var warehouses = await _warehousesApi.GetAllAsync();
            Warehouses.Clear();
            foreach (var w in warehouses)
                Warehouses.Add(w);

            if (Warehouses.Count > 0 && SelectedWarehouse is null)
                SelectedWarehouse = Warehouses[0];
        }
        catch { }
    }

    partial void OnSelectedWarehouseChanged(WarehouseDto? value)
    {
        if (value is not null)
            _ = LoadStocksAsync();
    }

    [RelayCommand]
    private async Task LoadStocksAsync()
    {
        if (SelectedWarehouse is null) return;

        IsLoading = true;
        try
        {
            var search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText;
            var stocks = await _stocksApi.GetAllAsync(SelectedWarehouse.Id, search);
            Stocks.Clear();
            foreach (var s in stocks)
                Stocks.Add(s);
        }
        catch { }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSearchTextChanged(string value) => _ = LoadStocksAsync();
}
