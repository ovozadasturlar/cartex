namespace Cartex.Application.Common.Settings;

public sealed class ProductReferenceSettings
{
    public bool IsEnabled { get; set; }
    public string SourceType { get; set; } = "GoogleSheets";
    public string SpreadsheetId { get; set; } = string.Empty;
    public string SheetName { get; set; } = string.Empty;
    public string BarcodeColumn { get; set; } = "barcode";
    public string NameColumn { get; set; } = "name";
    public string UnitColumn { get; set; } = "unit";
    public string CategoryColumn { get; set; } = "category";
    public string ManufacturerColumn { get; set; } = "manufacturer";
    public string PackQtyColumn { get; set; } = "pack_qty";
    public string PriceColumn { get; set; } = "price";
    public bool AutoFillPrice { get; set; }
    public string SyncSchedule { get; set; } = "Manual";
    public DateTime? LastSyncedAt { get; set; }
    public int LastReadCount { get; set; }
    public int LastUpdatedCount { get; set; }
    public int LastErrorCount { get; set; }
    public int RowCount { get; set; }
    public string? LastError { get; set; }

    public ProductReferenceSourceConfig ToSourceConfig() => new(
        SpreadsheetId,
        SheetName,
        BarcodeColumn,
        NameColumn,
        UnitColumn,
        CategoryColumn,
        ManufacturerColumn,
        PackQtyColumn,
        PriceColumn);
}

public sealed record ProductReferenceSourceConfig(
    string SpreadsheetId,
    string SheetName,
    string BarcodeColumn,
    string NameColumn,
    string UnitColumn,
    string CategoryColumn,
    string ManufacturerColumn,
    string PackQtyColumn,
    string PriceColumn);
