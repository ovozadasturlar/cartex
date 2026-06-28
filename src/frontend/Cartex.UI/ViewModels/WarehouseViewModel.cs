using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Stocks;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class WarehouseViewModel : ViewModelBase, ILoadable
{
    private readonly IStocksApi _stocksApi;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;

    public BranchContextService Branch { get; }

    private readonly List<StockOnHandDto> _allOnHand = [];

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _showExpiring;

    public ObservableCollection<StockOnHandDto> OnHand { get; } = [];
    public ObservableCollection<ExpiringStockDto> Expiring { get; } = [];

    public bool IsOnHandEmpty => OnHand.Count == 0;
    public bool IsExpiringEmpty => Expiring.Count == 0;

    public WarehouseViewModel(IStocksApi stocksApi, BranchContextService branch, IToastService toast, IBusyService busy)
    {
        _stocksApi = stocksApi;
        Branch = branch;
        _toast = toast;
        _busy = busy;
        Branch.PropertyChanged += OnBranchChanged;
    }

    public async Task LoadAsync()
    {
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                await LoadOnHandAsync();

                var expiring = await _stocksApi.GetExpiringAsync(30);
                Expiring.Clear();
                foreach (var e in expiring)
                    Expiring.Add(e);
                OnPropertyChanged(nameof(IsExpiringEmpty));
            }
        }
        catch
        {
            _toast.Error(L["error"]);
        }
    }

    private async Task LoadOnHandAsync()
    {
        var warehouseId = Branch.CurrentWarehouseId;
        _allOnHand.Clear();
        if (warehouseId is not null)
            _allOnHand.AddRange(await _stocksApi.GetOnHandAsync(warehouseId.Value));
        ApplyFilter();
    }

    private void OnBranchChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BranchContextService.SelectedWarehouse))
            _ = LoadOnHandAsync();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var query = SearchText.Trim();
        IEnumerable<StockOnHandDto> source = _allOnHand;
        if (!string.IsNullOrEmpty(query))
            source = source.Where(s => s.ProductName.Contains(query, StringComparison.OrdinalIgnoreCase));

        OnHand.Clear();
        foreach (var s in source)
            OnHand.Add(s);
        OnPropertyChanged(nameof(IsOnHandEmpty));
    }

    [RelayCommand]
    private void ShowOnHandTab() => ShowExpiring = false;

    [RelayCommand]
    private void ShowExpiringTab() => ShowExpiring = true;
}
