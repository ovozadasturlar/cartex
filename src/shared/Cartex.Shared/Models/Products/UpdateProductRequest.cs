namespace Cartex.Shared.Models.Products;

public record UpdateProductRequest(
    string Name,
    long? CategoryId,
    long UnitId,
    decimal MinStock,
    long? ProductTypeId = null,
    bool? TracksExpiryOverride = null,
    string? Attributes = null);
