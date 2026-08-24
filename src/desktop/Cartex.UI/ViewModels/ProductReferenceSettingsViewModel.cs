using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Products;
using Cartex.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.UI.ViewModels;

public partial class ProductReferenceSettingsViewModel : ViewModelBase, ILoadable
{
    private readonly ISettingsApi _settingsApi;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;

    public ObservableCollection<string> SyncSchedules { get; } = [];

    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] private string _spreadsheetId = string.Empty;
    [ObservableProperty] private string _sheetName = string.Empty;
    [ObservableProperty] private string _barcodeColumn = "barcode";
    [ObservableProperty] private string _nameColumn = "name";
    [ObservableProperty] private string _unitColumn = "unit";
    [ObservableProperty] private string _categoryColumn = "category";
    [ObservableProperty] private string _manufacturerColumn = "manufacturer";
    [ObservableProperty] private string _packQtyColumn = "pack_qty";
    [ObservableProperty] private string _priceColumn = "price";
    [ObservableProperty] private bool _autoFillPrice;
    [ObservableProperty] private int _syncScheduleIndex;
    [ObservableProperty] private DateTime? _lastSyncedAt;
    [ObservableProperty] private int _lastReadCount;
    [ObservableProperty] private int _lastUpdatedCount;
    [ObservableProperty] private int _lastErrorCount;
    [ObservableProperty] private int _rowCount;
    [ObservableProperty] private string? _lastError;

    public string LastSyncedText => LastSyncedAt?.ToLocalTime().ToString("g") ?? L["never"];

    public ProductReferenceSettingsViewModel(ISettingsApi settingsApi, IToastService toast, IBusyService busy)
    {
        _settingsApi = settingsApi;
        _toast = toast;
        _busy = busy;
        SyncSchedules.Add(L["sync_manual"]);
        SyncSchedules.Add(L["sync_daily"]);
    }

    public async Task LoadAsync()
    {
        try
        {
            ProductReferenceSettingsDto settings;
            using (_busy.Begin(L["loading"])) settings = await _settingsApi.GetProductReferenceAsync();
            Apply(settings);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            using (_busy.Begin(L["loading"])) await _settingsApi.UpdateProductReferenceAsync(ToDto());
            _toast.Success(L["success"]);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task SyncNowAsync()
    {
        try
        {
            ProductReferenceSyncResultDto result;
            using (_busy.Begin(L["loading"])) result = await _settingsApi.SyncProductReferenceAsync();
            LastSyncedAt = result.SyncedAt;
            LastReadCount = result.Read;
            LastUpdatedCount = result.Updated;
            LastErrorCount = result.Errors;
            RowCount = result.Total;
            LastError = null;
            OnPropertyChanged(nameof(LastSyncedText));
            _toast.Success(L["product_reference_sync_complete"]);
        }
        catch (Exception ex)
        {
            _toast.Error(ApiErrors.Describe(ex));
            await LoadAsync();
        }
    }

    private void Apply(ProductReferenceSettingsDto settings)
    {
        IsEnabled = settings.IsEnabled;
        SpreadsheetId = settings.SpreadsheetId;
        SheetName = settings.SheetName;
        BarcodeColumn = settings.BarcodeColumn;
        NameColumn = settings.NameColumn;
        UnitColumn = settings.UnitColumn;
        CategoryColumn = settings.CategoryColumn;
        ManufacturerColumn = settings.ManufacturerColumn;
        PackQtyColumn = settings.PackQtyColumn;
        PriceColumn = settings.PriceColumn;
        AutoFillPrice = settings.AutoFillPrice;
        SyncScheduleIndex = string.Equals(settings.SyncSchedule, "Daily", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        LastSyncedAt = settings.LastSyncedAt;
        LastReadCount = settings.LastReadCount;
        LastUpdatedCount = settings.LastUpdatedCount;
        LastErrorCount = settings.LastErrorCount;
        RowCount = settings.RowCount;
        LastError = settings.LastError;
        OnPropertyChanged(nameof(LastSyncedText));
    }

    private ProductReferenceSettingsDto ToDto() => new()
    {
        IsEnabled = IsEnabled,
        SourceType = "GoogleSheets",
        SpreadsheetId = SpreadsheetId.Trim(),
        SheetName = SheetName.Trim(),
        BarcodeColumn = BarcodeColumn.Trim(),
        NameColumn = NameColumn.Trim(),
        UnitColumn = UnitColumn.Trim(),
        CategoryColumn = CategoryColumn.Trim(),
        ManufacturerColumn = ManufacturerColumn.Trim(),
        PackQtyColumn = PackQtyColumn.Trim(),
        PriceColumn = PriceColumn.Trim(),
        AutoFillPrice = AutoFillPrice,
        SyncSchedule = SyncScheduleIndex == 1 ? "Daily" : "Manual"
    };
}
