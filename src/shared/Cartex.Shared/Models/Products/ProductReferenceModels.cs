namespace Cartex.Shared.Models.Products;

public sealed record ProductReferenceDto(
    string Barcode,
    string Name,
    string? UnitHint,
    string? CategoryHint,
    string? ManufacturerHint,
    decimal? PackQty,
    decimal? SuggestedPrice,
    string SourceKey,
    DateTime SyncedAt);

public sealed record ProductReferenceSettingsDto
{
    public bool IsEnabled { get; init; }
    public string SourceType { get; init; } = "GoogleSheets";
    public string SpreadsheetId { get; init; } = string.Empty;
    public string SheetName { get; init; } = string.Empty;
    public string BarcodeColumn { get; init; } = "barcode";
    public string NameColumn { get; init; } = "name";
    public string UnitColumn { get; init; } = "unit";
    public string CategoryColumn { get; init; } = "category";
    public string ManufacturerColumn { get; init; } = "manufacturer";
    public string PackQtyColumn { get; init; } = "pack_qty";
    public string PriceColumn { get; init; } = "price";
    public bool AutoFillPrice { get; init; }
    public string SyncSchedule { get; init; } = "Manual";
    public DateTime? LastSyncedAt { get; init; }
    public int LastReadCount { get; init; }
    public int LastUpdatedCount { get; init; }
    public int LastErrorCount { get; init; }
    public int RowCount { get; init; }
    public string? LastError { get; init; }
}

public sealed record ProductReferenceSyncResultDto(int Read, int Updated, int Errors, int Total, DateTime SyncedAt);
