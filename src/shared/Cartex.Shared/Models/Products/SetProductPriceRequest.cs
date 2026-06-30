namespace Cartex.Shared.Models.Products;

public record SetProductPriceRequest(long VariantId, long? WarehouseId, decimal SellingPrice);
