namespace Cartex.Shared.Models.Products;

public record ProductLookupDto(long VariantId, string ProductName, string UnitName, decimal PackQty, decimal SellingPrice, decimal OnHand, string Dimension, string? ImageKey = null, bool AllowsAmountEntry = false, decimal QuantityStep = 1);
