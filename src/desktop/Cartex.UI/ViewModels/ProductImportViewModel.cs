using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Products;
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
    private readonly IFilePickerService _filePicker;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private readonly ReferenceCache _cache;

    private byte[]? _file;

    public ObservableCollection<ImportColumnVm> Columns { get; } = [];
    public ObservableCollection<ImportRowVm> Rows { get; } = [];

    public string[] Fields { get; } =
    [
        "", "Name", "Barcode", "PackQty", "Sku", "Category", "Unit",
        "SellingPrice", "PurchasePrice", "Quantity", "ExpiredAt", "MinStock", "Ikpu", "Vat", "ImageUrl", "Currency"
    ];

    [ObservableProperty] private bool _isOpen;
    [ObservableProperty] private string? _fileName;
    [ObservableProperty] private bool _updatePrices;
    [ObservableProperty] private bool _createMissingCategories = true;
    [ObservableProperty] private int _createCount;
    [ObservableProperty] private int _existingCount;
    [ObservableProperty] private int _errorCount;

    public bool HasPreview => Rows.Count > 0;
    public bool HasErrors => ErrorCount > 0;

    public event Action? Imported;

    public ProductImportViewModel(IProductsApi productsApi, IFilePickerService filePicker,
        IToastService toast, IBusyService busy, ReferenceCache cache)
    {
        _productsApi = productsApi;
        _filePicker = filePicker;
        _toast = toast;
        _busy = busy;
        _cache = cache;
    }

    partial void OnErrorCountChanged(int value) => OnPropertyChanged(nameof(HasErrors));

    [RelayCommand]
    private void Open()
    {
        _file = null;
        FileName = null;
        UpdatePrices = false;
        CreateMissingCategories = true;
        Columns.Clear();
        Rows.Clear();
        CreateCount = ExistingCount = ErrorCount = 0;
        OnPropertyChanged(nameof(HasPreview));
        IsOpen = true;
    }

    [RelayCommand]
    private void Cancel() => IsOpen = false;

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

        try
        {
            using (_busy.Begin(L["loading"]))
            {
                var result = await _productsApi.ImportAsync(new ImportProductsRequest(
                    rows, UpdatePrices, CreateMissingCategories, IgnoreErrors: true));

                _cache.Invalidate(CacheKeys.ProductLookup);
                _cache.Invalidate(CacheKeys.Categories);
                IsOpen = false;
                var summary = $"{L["import_done"]}: {result.Created} + {result.Existing} · {result.BarcodesGenerated} {L["barcode"]}";
                if (result.ImagesSet > 0)
                    summary += $" · {L["image"]}: {result.ImagesSet}";
                _toast.Success(summary);
                if (result.ImagesFailed > 0)
                    _toast.Warning(string.Format(L["import_images_failed_fmt"], result.ImagesFailed));
                if (result.FailedRows is { Count: > 0 } failed)
                    _toast.Warning($"{failed.Count} ta qator import qilinmadi");
                Imported?.Invoke();
            }
        }
        catch (Exception ex)
        {
            _toast.Error(ApiErrors.Describe(ex));
        }
    }
}
