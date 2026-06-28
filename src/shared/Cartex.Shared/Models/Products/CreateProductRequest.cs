namespace Cartex.Shared.Models.Products;

public record CreateProductRequest(
    string Name,
    long? CategoryId,
    long UnitId,
    decimal MinStock,
    List<string>? Barcodes,
    long? ProductTypeId = null,
    bool? TracksExpiryOverride = null,
    string? Attributes = null);
