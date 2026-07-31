using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Categories;
using Cartex.Shared.Models.Common;
using Cartex.Shared.Models.Stocks;
using Cartex.UI.Services;
using Cartex.UI.ViewModels.Common;

namespace Cartex.UI.ViewModels;

public partial class WarehouseViewModel : ViewModelBase, ILoadable, IDisposable
{
    private readonly IStocksApi _stocksApi;
    private readonly ICategoriesApi _categoriesApi;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private readonly IExportService _export;
    private readonly AuthService _auth;

    private bool _suppressReload;

    public BranchContextService Branch { get; }
    public bool CanExport => _auth.HasPermission("reports.export");
    public bool CanAdjust => _auth.HasPermission("stocks.adjust");

    public PaginationState Paging { get; } = new();

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _tab = "onhand";
    [ObservableProperty] private CategoryDto? _filterCategory;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private decimal _totalQuantity;
    [ObservableProperty] private decimal _totalValue;
    [ObservableProperty] private AdjustStockDialogViewModel? _adjustVm;

    public bool IsModalOpen => AdjustVm is not null;

    partial void OnAdjustVmChanged(AdjustStockDialogViewModel? value) => OnPropertyChanged(nameof(IsModalOpen));

    public bool IsOnHand => Tab == "onhand";
    public bool IsLowStock => Tab == "low";
    public bool IsExpiring => Tab == "expiring";

    public ObservableCollection<StockOnHandDto> OnHand { get; } = [];
    public ObservableCollection<LowStockDto> LowStock { get; } = [];
    public ObservableCollection<ExpiringStockDto> Expiring { get; } = [];
    public ObservableCollection<CategoryDto> FilterCategories { get; } = [];

    public bool IsOnHandEmpty => OnHand.Count == 0;
    public bool IsLowStockEmpty => LowStock.Count == 0;
    public bool IsExpiringEmpty => Expiring.Count == 0;

    public int LowStockCount => LowStock.Count;
    public decimal LowStockShortage => LowStock.Sum(i => Math.Max(0, i.MinStock - i.OnHand));
    public int ExpiringCount => Expiring.Count;
    public decimal ExpiringQuantity => Expiring.Sum(e => e.Quantity);

    public WarehouseViewModel(IStocksApi stocksApi, ICategoriesApi categoriesApi, BranchContextService branch, IToastService toast, IBusyService busy, IExportService export, AuthService auth)
    {
        _stocksApi = stocksApi;
        _categoriesApi = categoriesApi;
        Branch = branch;
        _toast = toast;
        _busy = busy;
        _export = export;
        _auth = auth;
        Paging.Attach(LoadOnHandAsync);
        Branch.PropertyChanged += OnBranchChanged;
    }

    public void Dispose() => Branch.PropertyChanged -= OnBranchChanged;

    [RelayCommand]
    private async Task Export(string format)
    {
        var warehouseId = Branch.CurrentWarehouseId;
        if (warehouseId is null) return;
        try
        {
            var search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim();
            long? categoryId = FilterCategory is { Id: > 0 } ? FilterCategory.Id : null;
            var all = await _stocksApi.GetOnHandAsync(warehouseId.Value, categoryId, search, 0, 0);
            await _export.ExportAsync(L["inventory"], all.Items,
            [
                new(L["product_name"], s => s.ProductName),
                new(L["category"], s => s.CategoryName),
                new(L["unit"], s => s.UnitName),
                new(L["on_hand"], s => s.Quantity),
                new(L["selling_price"], s => s.SellingPrice),
                new(L["expiring"], s => s.NearestExpiry?.ToString("yyyy-MM-dd")),
            ], Enum.Parse<ExportFormat>(format));
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    public async Task LoadAsync()
    {
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                var categoriesTask = ServiceLocator.Resolve<ReferenceCache>()
                    .GetAsync(CacheKeys.Categories, () => _categoriesApi.GetAllAsync());
                var expiringTask = _stocksApi.GetExpiringAsync(30);
                var onHandTask = LoadOnHandAsync();
                var lowStockTask = LoadLowStockAsync();

                _suppressReload = true;
                var categories = await categoriesTask;
                FilterCategories.Clear();
                FilterCategories.Add(new CategoryDto(0, L["all"], null, null, null));
                foreach (var c in categories) FilterCategories.Add(c);
                FilterCategory = FilterCategories[0];
                _suppressReload = false;

                await Task.WhenAll(onHandTask, lowStockTask);

                var expiring = await expiringTask;
                Expiring.Clear();
                foreach (var e in expiring)
                    Expiring.Add(e);
                OnPropertyChanged(nameof(IsExpiringEmpty));
                OnPropertyChanged(nameof(ExpiringCount));
                OnPropertyChanged(nameof(ExpiringQuantity));
            }
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    private async Task LoadOnHandAsync()
    {
        var warehouseId = Branch.CurrentWarehouseId;
        OnHand.Clear();
        if (warehouseId is null)
        {
            TotalCount = 0; TotalQuantity = 0; TotalValue = 0;
            OnPropertyChanged(nameof(IsOnHandEmpty));
            return;
        }

        var search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim();
        long? categoryId = FilterCategory is { Id: > 0 } ? FilterCategory.Id : null;
        var result = await _stocksApi.GetOnHandAsync(warehouseId.Value, categoryId, search, Paging.Page, Paging.PageSize);

        foreach (var s in result.Items) OnHand.Add(s);
        TotalCount = result.TotalCount;
        TotalQuantity = result.TotalQuantity;
        TotalValue = result.TotalValue;
        var totalPages = Math.Max(1, (int)Math.Ceiling(result.TotalCount / (double)Paging.PageSize));
        Paging.Apply(new PagedListMetadata(result.TotalCount, Paging.Page, Paging.PageSize, totalPages));
        OnPropertyChanged(nameof(IsOnHandEmpty));
    }

    private async Task LoadLowStockAsync()
    {
        var warehouseId = Branch.CurrentWarehouseId;
        LowStock.Clear();
        if (warehouseId is not null)
        {
            var items = await _stocksApi.GetLowStockAsync(warehouseId.Value);
            foreach (var i in items) LowStock.Add(i);
        }
        OnPropertyChanged(nameof(IsLowStockEmpty));
        OnPropertyChanged(nameof(LowStockCount));
        OnPropertyChanged(nameof(LowStockShortage));
    }

    private void OnBranchChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BranchContextService.SelectedWarehouse))
        {
            Paging.Page = 1;
            _ = LoadOnHandAsync();
            _ = LoadLowStockAsync();
        }
    }

    private CancellationTokenSource? _searchCts;

    partial void OnSearchTextChanged(string value)
    {
        if (_suppressReload) return;
        _searchCts?.Cancel();
        var cts = _searchCts = new CancellationTokenSource();
        _ = DebouncedSearchAsync(cts.Token);
    }

    private async Task DebouncedSearchAsync(CancellationToken token)
    {
        try { await Task.Delay(300, token); } catch { return; }
        if (token.IsCancellationRequested) return;
        Paging.Page = 1;
        await LoadOnHandAsync();
    }
    partial void OnFilterCategoryChanged(CategoryDto? value) { if (_suppressReload) return; Paging.Page = 1; _ = LoadOnHandAsync(); }

    partial void OnTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsOnHand));
        OnPropertyChanged(nameof(IsLowStock));
        OnPropertyChanged(nameof(IsExpiring));
    }

    [RelayCommand]
    private void ShowOnHandTab() => Tab = "onhand";

    [RelayCommand]
    private void ShowLowStockTab() => Tab = "low";

    [RelayCommand]
    private void ShowExpiringTab() => Tab = "expiring";

    [RelayCommand]
    private async Task ExportLowStock(string format)
    {
        var warehouseId = Branch.CurrentWarehouseId;
        if (warehouseId is null) return;
        try
        {
            var all = await _stocksApi.GetLowStockAsync(warehouseId.Value);
            await _export.ExportAsync(L["low_stock"], all,
            [
                new(L["product_name"], s => s.ProductName),
                new(L["unit"], s => s.UnitName),
                new(L["warehouse"], s => s.WarehouseName),
                new(L["on_hand"], s => s.OnHand),
                new(L["min_stock"], s => s.MinStock),
            ], Enum.Parse<ExportFormat>(format));
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private void Adjust(StockOnHandDto item)
    {
        if (item is null || Branch.CurrentWarehouseId is null) return;
        var vm = new AdjustStockDialogViewModel(item.ProductName, item.UnitName, item.Quantity);
        vm.RequestClose += async (_, result) =>
        {
            AdjustVm = null;
            if (result is AdjustStockResult r) await ApplyAdjustAsync(item, r);
        };
        AdjustVm = vm;
    }

    [RelayCommand]
    private void CloseAdjust() => AdjustVm = null;

    private async Task ApplyAdjustAsync(StockOnHandDto item, AdjustStockResult result)
    {
        var warehouseId = Branch.CurrentWarehouseId;
        if (warehouseId is null) return;
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                await _stocksApi.AdjustAsync(new AdjustStockRequest(warehouseId.Value, item.VariantId, result.CountedQuantity, result.Reason));
                await LoadOnHandAsync();
                await LoadLowStockAsync();
            }
            _toast.Success(L["success"]);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }
}
