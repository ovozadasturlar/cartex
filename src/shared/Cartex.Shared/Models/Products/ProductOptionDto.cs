namespace Cartex.Shared.Models.Products;

public record ProductOptionDto(
    long Id,
    long DefaultVariantId,
    string Name,
    string? Dimension,
    long? UnitId = null,
    string? UnitShortName = null,
    IReadOnlyCollection<ProductPackDto>? Packs = null,
    string? ImageKey = null);
