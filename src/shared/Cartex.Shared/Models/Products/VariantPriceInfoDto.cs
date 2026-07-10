namespace Cartex.Shared.Models.Products;

public record VariantPriceInfoDto(decimal? LastPurchasePrice, decimal? SellingPrice, long? LastUnitId = null, decimal? LastPackSize = null);
