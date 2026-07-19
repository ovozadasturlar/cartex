namespace Cartex.Application.Products.Import;

public enum ImportRowAction
{
    Create,
    Existing,
    Skip
}

public enum ImportStockMode
{
    None,
    Supply,
    Opening
}

public record ImportRowDto(
    int Row,
    string? Name,
    string? Barcode,
    decimal? PackQty,
    string? Sku,
    string? Category,
    string? Unit,
    decimal? SellingPrice,
    decimal? PurchasePrice,
    decimal? Quantity,
    DateOnly? ExpiredAt,
    decimal? MinStock,
    string? Ikpu,
    decimal? Vat,
    long? VariantId,
    ImportRowAction Action,
    List<string> Errors,
    List<string> Warnings);

public record ProductImportPreviewDto(
    List<string> Columns,
    Dictionary<int, string> Mapping,
    List<ImportRowDto> Rows,
    int CreateCount,
    int ExistingCount,
    int ErrorCount);

public record ImportResultDto(int Created, int Existing, int BarcodesGenerated, long? SupplyId, int StockAdjusted);
