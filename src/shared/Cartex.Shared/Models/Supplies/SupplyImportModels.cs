namespace Cartex.Shared.Models.Supplies;

public record SupplyImportRowDto(
    int Row,
    long? VariantId,
    string? Name,
    string? Barcode,
    decimal Quantity,
    decimal? PurchasePrice,
    decimal? SellingPrice,
    DateOnly? ExpiredAt,
    string? Message);

public record SupplyImportPreviewDto(List<SupplyImportRowDto> Rows, int MatchedCount, int UnmatchedCount);
