using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Paging;
using Cartex.Shared.Models.StockTransfers;
using Cartex.UI.Models;
using Cartex.UI.Services;
using Cartex.UI.ViewModels.Common;

namespace Cartex.UI.ViewModels;

public partial class TransfersViewModel : ViewModelBase, ILoadable
{
    private readonly IStockTransfersApi _api;
    private readonly IWarehousesApi _warehousesApi;
    private readonly IProductsApi _productsApi;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private readonly IExportService _export;
    private readonly AuthService _auth;
    private readonly ReferenceCache _cache;

    public ObservableCollection<StockTransferDto> Transfers { get; } = [];
    public ObservableCollection<IdOption> WarehouseOptions { get; } = [];
    public ObservableCollection<IdOption> FilterWarehouseOptions { get; } = [];
    public ObservableCollection<IdOption> ProductOptions { get; } = [];

    [ObservableProperty] private bool _isEditOpen;
    [ObservableProperty] private IdOption? _fromWarehouse;
    [ObservableProperty] private IdOption? _toWarehouse;
    [ObservableProperty] private IdOption? _product;
    [ObservableProperty] private decimal _quantity = 1;

    [ObservableProperty] private DateTimeOffset _dateFrom = DateTimeOffset.Now.AddDays(-30);
    [ObservableProperty] private DateTimeOffset _dateTo = DateTimeOffset.Now;
    [ObservableProperty] private IdOption? _filterWarehouse;

    [ObservableProperty] private StockTransferDto? _selectedTransfer;
    [ObservableProperty] private bool _isDetailOpen;

    public bool IsModalOpen => IsEditOpen || IsDetailOpen;
    partial void OnIsEditOpenChanged(bool value) => OnPropertyChanged(nameof(IsModalOpen));
    partial void OnIsDetailOpenChanged(bool value) => OnPropertyChanged(nameof(IsModalOpen));

    public PaginationState Paging { get; } = new();
    [ObservableProperty] private StockTransfersTotalsDto? _totals;
    public bool IsEmpty => Transfers.Count == 0;
    public bool CanExport => _auth.HasPermission("reports.export");

    public TransfersViewModel(IStockTransfersApi api, IWarehousesApi warehousesApi, IProductsApi productsApi, IToastService toast, IBusyService busy, IExportService export, AuthService auth, ReferenceCache cache)
    {
        _cache = cache;
        _api = api;
        _warehousesApi = warehousesApi;
        _productsApi = productsApi;
        _toast = toast;
        _busy = busy;
        _export = export;
        _auth = auth;
        Paging.Attach(LoadTransfersAsync);
    }

    private async Task LoadTransfersAsync()
    {
        try
        {
            var from = new DateTimeOffset(DateFrom.Date).UtcDateTime;
            var to = new DateTimeOffset(DateTo.Date.AddDays(1)).UtcDateTime;
            var pagedTask = _api.GetPagedAsync(Paging.Page, Paging.PageSize, Paging.SortBy, Paging.Descending, null, FilterWarehouse?.Id, from, to);
            var totalsTask = _api.GetTotalsAsync(FilterWarehouse?.Id, from, to);
            var paged = (await pagedTask).ToPaged();
            Transfers.Clear();
            foreach (var t in paged.Items) Transfers.Add(t);
            Paging.Apply(paged.Meta);
            Totals = await totalsTask;
            OnPropertyChanged(nameof(IsEmpty));
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    public async Task LoadAsync()
    {
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                var warehousesTask = _cache.GetAsync(CacheKeys.Warehouses, () => _warehousesApi.GetAllAsync());
                var productsTask = _cache.GetAsync(CacheKeys.ProductLookup, _productsApi.GetLookupAsync);

                var warehouses = await warehousesTask;
                WarehouseOptions.Clear();
                FilterWarehouseOptions.Clear();
                FilterWarehouseOptions.Add(new IdOption(null, L["all"]));
                foreach (var w in warehouses)
                {
                    WarehouseOptions.Add(new IdOption(w.Id, w.Name));
                    FilterWarehouseOptions.Add(new IdOption(w.Id, w.Name));
                }
                FilterWarehouse = FilterWarehouseOptions.FirstOrDefault();

                var products = await productsTask;
                ProductOptions.Clear();
                foreach (var p in products) ProductOptions.Add(new IdOption(p.DefaultVariantId, p.Name));

                await LoadTransfersAsync();
            }
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private Task Refresh() { Paging.Page = 1; return LoadTransfersAsync(); }

    [RelayCommand]
    private async Task Export(string format)
    {
        try
        {
            var from = new DateTimeOffset(DateFrom.Date).UtcDateTime;
            var to = new DateTimeOffset(DateTo.Date.AddDays(1)).UtcDateTime;
            var all = await _api.GetAllAsync(FilterWarehouse?.Id, from, to);
            await _export.ExportAsync(L["transfers"], all,
            [
                new(L["date"], t => t.CreatedAt),
                new(L["from_account"], t => t.FromWarehouse),
                new(L["to_account"], t => t.ToWarehouse),
                new(L["product_name"], t => t.ProductName),
                new(L["quantity"], t => t.Quantity),
                new(L["status"], t => t.Status),
            ], Enum.Parse<ExportFormat>(format));
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private void OpenDetail(StockTransferDto transfer)
    {
        SelectedTransfer = transfer;
        IsDetailOpen = true;
    }

    [RelayCommand]
    private void CloseDetail() => IsDetailOpen = false;

    [RelayCommand]
    private void OpenCreate()
    {
        FromWarehouse = WarehouseOptions.FirstOrDefault();
        ToWarehouse = WarehouseOptions.Skip(1).FirstOrDefault() ?? WarehouseOptions.FirstOrDefault();
        Product = ProductOptions.FirstOrDefault();
        Quantity = 1;
        IsEditOpen = true;
    }

    [RelayCommand]
    private void CancelEdit() => IsEditOpen = false;

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (FromWarehouse?.Id is null || ToWarehouse?.Id is null || Product?.Id is null
            || FromWarehouse.Id == ToWarehouse.Id || Quantity <= 0) { _toast.Error(L["error"]); return; }
        try
        {
            using (_busy.Begin(L["loading"]))
                await _api.CreateAsync(new CreateStockTransferRequest(FromWarehouse.Id.Value, ToWarehouse.Id.Value, Product.Id.Value, Quantity));
            IsEditOpen = false;
            _toast.Success(L["success"]);
            await LoadAsync();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task ReceiveAsync(StockTransferDto transfer)
    {
        try
        {
            using (_busy.Begin(L["loading"]))
                await _api.ReceiveAsync(transfer.Id);
            _toast.Success(L["success"]);
            await LoadAsync();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }
}
