using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Querying;
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
    [ObservableProperty] private IReadOnlyList<IdOption> _productOptions = [];
    private Task? _productCatalogTask;

    [ObservableProperty] private bool _isEditOpen;
    [ObservableProperty] private IdOption? _fromWarehouse;
    [ObservableProperty] private IdOption? _toWarehouse;
    [ObservableProperty] private IdOption? _product;
    [ObservableProperty] private string _productText = "";
    [ObservableProperty] private decimal _quantity = 1;

    private IReadOnlyList<PageShortcut>? _shortcuts;
    public IReadOnlyList<PageShortcut> Shortcuts => _shortcuts ??= CrudShortcuts(OpenCreateCommand, SaveCommand, () => IsEditOpen = false, () => IsEditOpen);

    partial void OnProductChanged(IdOption? value)
    {
        if (value is not null && ProductText != value.Name) ProductText = value.Name;
    }

    [RelayCommand]
    private void CommitProduct()
    {
        var name = ProductText.Trim();
        if (name.Length == 0) return;
        if (ProductOptions.FirstOrDefault(o => string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase)) is { } match)
            Product = match;
    }

    [ObservableProperty] private DateTimeOffset _dateFrom = DateTimeOffset.Now.AddDays(-30);
    [ObservableProperty] private DateTimeOffset _dateTo = DateTimeOffset.Now;
    [ObservableProperty] private IdOption? _filterWarehouse;

    [ObservableProperty] private StockTransferDto? _selectedTransfer;
    [ObservableProperty] private bool _isDetailOpen;
    private static readonly StockTransferDto EmptyTransfer = new(0, "", 0, "", "", "", DateTime.MinValue, "");
    public StockTransferDto SelectedTransferDisplay => SelectedTransfer ?? EmptyTransfer;

    public bool IsModalOpen => IsEditOpen || IsDetailOpen;
    partial void OnIsEditOpenChanged(bool value) => OnPropertyChanged(nameof(IsModalOpen));
    partial void OnIsDetailOpenChanged(bool value) => OnPropertyChanged(nameof(IsModalOpen));

    public PaginationState Paging { get; } = new();
    [ObservableProperty] private StockTransfersTotalsDto _totals = new(0, 0);
    public bool IsEmpty => Transfers.Count == 0;
    public bool CanExport => _auth.HasPermission("reports.export");
    public bool CanCreate => _auth.HasPermission("stock_transfers.create");
    public bool CanReceive => _auth.HasPermission("stock_transfers.receive");

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
        _auth.LoggedOut += ResetState;
    }

    private void ResetState()
    {
        Transfers.Clear();
        SelectedTransfer = null;
        IsEditOpen = false;
        IsDetailOpen = false;
        FilterWarehouse = null;
        DateFrom = DateTimeOffset.Now.AddDays(-30);
        DateTo = DateTimeOffset.Now;
        Totals = new StockTransfersTotalsDto(0, 0);
        Paging.Page = 1;
        OnPropertyChanged(nameof(IsEmpty));
    }

    private async Task LoadTransfersAsync()
    {
        try
        {
            var from = new DateTimeOffset(DateFrom.Date).UtcDateTime;
            var to = new DateTimeOffset(DateTo.Date.AddDays(1)).UtcDateTime;
            var pagedTask = _api.QueryAsync(QueryRequest.Create()
                .Page(Paging.Page, Paging.PageSize)
                .Sort(Paging.SortBy, Paging.Descending)
                .With("warehouseId", FilterWarehouse?.Id)
                .With("fromDate", from)
                .With("toDate", to)
                .Build());
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

    private Task EnsureProductCatalogAsync() => _productCatalogTask ??= LoadProductCatalogAsync();

    private async Task LoadProductCatalogAsync()
    {
        try
        {
            var products = await _cache.GetAsync(CacheKeys.ProductLookup, _productsApi.GetLookupAsync);
            ProductOptions = products.Select(product => new IdOption(product.DefaultVariantId, product.Name)).ToList();
        }
        finally
        {
            _productCatalogTask = null;
        }
    }

    private void RaisePermissions()
    {
        OnPropertyChanged(nameof(CanExport));
        OnPropertyChanged(nameof(CanCreate));
        OnPropertyChanged(nameof(CanReceive));
    }

    public async Task LoadAsync()
    {
        RaisePermissions();
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                var warehousesTask = _cache.GetAsync(CacheKeys.Warehouses, () => _warehousesApi.GetAllAsync());
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
    private async Task OpenCreateAsync()
    {
        if (!CanCreate) return;
        await EnsureProductCatalogAsync();
        FromWarehouse = WarehouseOptions.FirstOrDefault();
        ToWarehouse = WarehouseOptions.Skip(1).FirstOrDefault() ?? WarehouseOptions.FirstOrDefault();
        Product = ProductOptions.FirstOrDefault();
        Quantity = 1;
        IsEditOpen = true;
    }

    partial void OnSelectedTransferChanged(StockTransferDto? value) => OnPropertyChanged(nameof(SelectedTransferDisplay));

    [RelayCommand]
    private void CancelEdit() => IsEditOpen = false;

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!CanCreate) return;
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
        if (!CanReceive) return;
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
