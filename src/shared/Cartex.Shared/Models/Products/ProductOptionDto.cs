namespace Cartex.Shared.Models.Products;

public record ProductOptionDto(long Id, long DefaultVariantId, string Name, string? Dimension);
