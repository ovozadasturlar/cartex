namespace Cartex.Shared.Models.Products;

public record ProductDto(
    long Id,
    long DefaultVariantId,
    string Name,
    string? CategoryName,
    string UnitName,
    decimal MinStock,
    List<string> Barcodes,
    long? ProductTypeId,
    string? ProductTypeName,
    bool TracksExpiry,
    string? Attributes,
    string? ImageKey,
    string? Code,
    string? IkpuCode,
    decimal? VatRate,
    decimal? SellingPrice,
    decimal OnHand = 0,
    string? ImageUrl = null,
    string? PriceCurrency = null,
    string? Dimension = null,
    long? ManufacturerId = null);
