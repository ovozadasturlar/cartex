namespace Cartex.Shared.Models.Products;

public record CreateProductRequest(
    string Name,
    long? CategoryId,
    long UnitId,
    decimal? MinStock,
    List<BarcodeInput>? Barcodes,
    long? ProductTypeId = null,
    bool? TracksExpiryOverride = null,
    string? Attributes = null,
    string? ImageKey = null,
    string? Code = null,
    string? IkpuCode = null,
    decimal? VatRate = null,
    decimal? SellingPrice = null,
    string? PriceCurrency = null);
