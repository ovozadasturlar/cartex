using System.Collections.ObjectModel;
using System.IO;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Products;
using Cartex.Shared.Models.Suppliers;
using Cartex.Shared.Models.Warehouses;
using Cartex.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Refit;

namespace Cartex.UI.ViewModels;

public sealed partial class ImportColumnVm(int index, string header, string? field) : ObservableObject
{
    public int Index { get; } = index;
    public string Header { get; } = header;
    [ObservableProperty] private string? _field = field;
}

public sealed partial class ImportRowVm(ImportRowDto row) : ObservableObject
{
    public ImportRowDto Row { get; } = row;

    [ObservableProperty] private bool _isSelected = row.Action != ImportRowAction.Skip;

    public int Number => Row.Row;
    public string? Name => Row.Name;
    public string? Barcode => Row.Barcode;
    public decimal? Quantity => Row.Quantity;
    public decimal? PurchasePrice => Row.PurchasePrice;
    public decimal? SellingPrice => Row.SellingPrice;
    public bool HasError => Row.Errors.Count > 0;
    public ImportRowAction Action => Row.Action;
    public string Message => string.Join(" · ", Row.Errors.Concat(Row.Warnings));
}

public partial class ProductImportViewModel : ViewModelBase
{
    private readonly IProductsApi _productsApi;
    private readonly IWarehousesApi _warehousesApi;
    private readonly ISuppliersApi _suppliersApi;
    private readonly IFilePickerService _filePicker;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private readonly AuthService _auth;
    private readonly ReferenceCache _cache;

    private byte[]? _file;

    public ObservableCollection<ImportColumnVm> Columns { get; } = [];
    public ObservableCollection<ImportRowVm> Rows { get; } = [];
    public ObservableCollection<WarehouseDto> Warehouses { get; } = [];
    public ObservableCollection<SupplierDto> Suppliers { get; } = [];

    public string[] Fields { get; } =
    [
        "", "Name", "Barcode", "PackQty", "Sku", "Category", "Unit",
        "SellingPrice", "PurchasePrice", "Quantity", "ExpiredAt", "MinStock", "Ikpu", "Vat"
    ];

    [ObservableProperty] private bool _isOpen;
    [ObservableProperty] private string? _fileName;
    [ObservableProperty] private ImportStockMode _mode = ImportStockMode.None;
    [ObservableProperty] private WarehouseDto? _selectedWarehouse;
    [ObservableProperty] private SupplierDto? _selectedSupplier;
    [ObservableProperty] private bool _updatePrices;
    [ObservableProperty] private bool _createMissingCategories = true;
    [ObservableProperty] private int _createCount;
    [ObservableProperty] private int _existingCount;
    [ObservableProperty] private int _errorCount;

    public bool HasPreview => Rows.Count > 0;
    public bool HasErrors => ErrorCount > 0;
    public bool IsCatalog => Mode == ImportStockMode.None;
    public bool IsSupply => Mode == ImportStockMode.Supply;
    public bool IsOpening => Mode == ImportStockMode.Opening;
    public bool NeedsWarehouse => Mode != ImportStockMode.None;

    public event Action? Imported;

    public ProductImportViewModel(IProductsApi productsApi, IWarehousesApi warehousesApi, ISuppliersApi suppliersApi,
        IFilePickerService filePicker, IToastService toast, IBusyService busy, AuthService auth, ReferenceCache cache)
    {
        _productsApi = productsApi;
        _warehousesApi = warehousesApi;
        _suppliersApi = suppliersApi;
        _filePicker = filePicker;
        _toast = toast;
        _busy = busy;
        _auth = auth;
        _cache = cache;
    }

    partial void OnModeChanged(ImportStockMode value)
    {
        OnPropertyChanged(nameof(IsCatalog));
        OnPropertyChanged(nameof(IsSupply));
        OnPropertyChanged(nameof(IsOpening));
        OnPropertyChanged(nameof(NeedsWarehouse));
    }

    partial void OnErrorCountChanged(int value) => OnPropertyChanged(nameof(HasErrors));

    [RelayCommand]
    private async Task Open()
    {
        _file = null;
        FileName = null;
        Mode = ImportStockMode.None;
        UpdatePrices = false;
        CreateMissingCategories = true;
        Columns.Clear();
        Rows.Clear();
        CreateCount = ExistingCount = ErrorCount = 0;
        OnPropertyChanged(nameof(HasPreview));

        try
        {
            if (Warehouses.Count == 0)
                foreach (var w in await _cache.GetAsync(CacheKeys.Warehouses, () => _warehousesApi.GetAllAsync()))
                    Warehouses.Add(w);
            if (Suppliers.Count == 0 && _auth.HasPermission("suppliers.view"))
                foreach (var s in await _suppliersApi.GetAllAsync())
                    Suppliers.Add(s);
        }
        catch (Exception ex)
        {
            _toast.Error(ApiErrors.Describe(ex));
        }

        SelectedWarehouse = Warehouses.FirstOrDefault();
        IsOpen = true;
    }

    [RelayCommand]
    private void Cancel() => IsOpen = false;

    [RelayCommand]
    private void SetMode(string mode) => Mode = Enum.Parse<ImportStockMode>(mode);

    [RelayCommand]
    private async Task PickFile()
    {
        try
        {
            var picked = await _filePicker.PickSpreadsheetAsync();
            if (picked is null)
                return;

            using var buffer = new MemoryStream();
            await using (picked.Content)
                await picked.Content.CopyToAsync(buffer);

            _file = buffer.ToArray();
            FileName = picked.FileName;
        }
        catch (Exception ex)
        {
            _toast.Error(ApiErrors.Describe(ex));
            return;
        }

        await PreviewAsync(null);
    }

    [RelayCommand]
    private Task Remap() => PreviewAsync(string.Join(',',
        Columns.Where(c => !string.IsNullOrEmpty(c.Field)).Select(c => $"{c.Index}:{c.Field}")));

    private async Task PreviewAsync(string? mapping)
    {
        if (_file is null)
            return;

        try
        {
            using (_busy.Begin(L["loading"]))
            {
                using var stream = new MemoryStream(_file);
                var preview = await _productsApi.PreviewImportAsync(
                    new StreamPart(stream, FileName ?? "import.xlsx",
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
                    mapping);

                Columns.Clear();
                for (var i = 0; i < preview.Columns.Count; i++)
                    Columns.Add(new ImportColumnVm(i, preview.Columns[i], preview.Mapping.GetValueOrDefault(i)));

                Rows.Clear();
                foreach (var row in preview.Rows)
                    Rows.Add(new ImportRowVm(row));

                CreateCount = preview.CreateCount;
                ExistingCount = preview.ExistingCount;
                ErrorCount = preview.ErrorCount;
                OnPropertyChanged(nameof(HasPreview));
            }
        }
        catch (Exception ex)
        {
            _toast.Error(ApiErrors.Describe(ex));
        }
    }

    [RelayCommand]
    private async Task DownloadTemplate()
    {
        try
        {
            await using var content = await _productsApi.GetImportTemplateAsync();
            var target = await _filePicker.SaveFileAsync("cartex-import", "xlsx");
            if (target is null)
                return;

            await using (target)
                await content.CopyToAsync(target);
            _toast.Success(L["success"]);
        }
        catch (Exception ex)
        {
            _toast.Error(ApiErrors.Describe(ex));
        }
    }

    [RelayCommand]
    private async Task Import()
    {
        var rows = Rows.Where(r => r.IsSelected && !r.HasError).Select(r => r.Row).ToList();
        if (rows.Count == 0)
        {
            _toast.Warning(L["import_no_rows"]);
            return;
        }

        if (NeedsWarehouse && SelectedWarehouse is null)
        {
            _toast.Warning(L["warehouse"]);
            return;
        }

        if (IsSupply && SelectedSupplier is null)
        {
            _toast.Warning(L["supplier"]);
            return;
        }

        try
        {
            using (_busy.Begin(L["loading"]))
            {
                var result = await _productsApi.ImportAsync(new ImportProductsRequest(
                    rows,
                    Mode,
                    NeedsWarehouse ? SelectedWarehouse!.Id : null,
                    IsSupply ? SelectedSupplier!.Id : null,
                    DateOnly.FromDateTime(DateTime.Today),
                    UpdatePrices: UpdatePrices,
                    CreateMissingCategories: CreateMissingCategories));

                _cache.Invalidate(CacheKeys.ProductLookup);
                _cache.Invalidate(CacheKeys.Categories);
                IsOpen = false;
                _toast.Success($"{L["import_done"]}: {result.Created} + {result.Existing} · {result.BarcodesGenerated} {L["barcode"]}");
                Imported?.Invoke();
            }
        }
        catch (Exception ex)
        {
            _toast.Error(ApiErrors.Describe(ex));
        }
    }
}
