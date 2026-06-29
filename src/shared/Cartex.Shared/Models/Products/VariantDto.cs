namespace Cartex.Shared.Models.Products;

public record VariantDto(long Id, long ProductId, string? Name, string? Code, string? Attributes, string? ImageKey, bool IsDefault, List<string> Barcodes);

public record CreateVariantRequest(string? Name, string? Code, string? Attributes, string? ImageKey, List<string>? Barcodes);

public record UpdateVariantRequest(string? Name, string? Code, string? Attributes, string? ImageKey, List<string>? Barcodes);
