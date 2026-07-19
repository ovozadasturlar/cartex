namespace Cartex.Shared.Models.Products;

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

public record ImportProductsRequest(
    List<ImportRowDto> Rows,
    ImportStockMode StockMode = ImportStockMode.None,
    long? WarehouseId = null,
    long? SupplierId = null,
    DateOnly? SupplyDate = null,
    decimal PaidCash = 0,
    decimal PaidCard = 0,
    string? Currency = null,
    bool UpdatePrices = false,
    bool CreateMissingCategories = true);

public record ImportResultDto(int Created, int Existing, int BarcodesGenerated, long? SupplyId, int StockAdjusted);
