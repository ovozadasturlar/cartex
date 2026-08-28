namespace Cartex.Catalog.Tool.Normalization;

public sealed record SourceRow(
    string Source,
    int RowNumber,
    string Barcode,
    string Name,
    string Manufacturer,
    CategoryPath Category,
    string Model,
    string Unit,
    string PackQuantity,
    ImageReference Image);
