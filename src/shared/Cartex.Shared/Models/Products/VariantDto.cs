namespace Cartex.Shared.Models.Products;

public record VariantBarcodeDto(string Code, decimal PackQty);

public record VariantDto(long Id, long ProductId, string? Name, string? Code, string? Attributes, string? ImageKey, bool IsDefault, List<VariantBarcodeDto> Barcodes);

public record CreateVariantRequest(string? Name, string? Code, string? Attributes, string? ImageKey, List<BarcodeInput>? Barcodes);

public record UpdateVariantRequest(string? Name, string? Code, string? Attributes, string? ImageKey, List<BarcodeInput>? Barcodes);
