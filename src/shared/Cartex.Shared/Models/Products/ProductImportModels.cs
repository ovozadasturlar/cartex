namespace Cartex.Shared.Models.Products;

public enum ImportRowAction
{
    Create,
    Existing,
    Skip
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
    string? ImageUrl,
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
    bool UpdatePrices = false,
    bool CreateMissingCategories = true);

public record ImportResultDto(int Created, int Existing, int BarcodesGenerated, int ImagesSet, int ImagesFailed);
