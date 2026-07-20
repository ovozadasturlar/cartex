namespace Cartex.Shared.Models.Stocks;

public record StockOnHandDto(long VariantId, string ProductName, long? CategoryId, string? CategoryName, string UnitName, string Dimension, decimal Quantity, decimal SellingPrice, DateOnly? NearestExpiry, string? ImageUrl = null, decimal? DiscountPct = null, string? Code = null, List<string>? Barcodes = null);
