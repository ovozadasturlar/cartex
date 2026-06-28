namespace Cartex.Shared.Models.Stocks;

public record StockOnHandDto(long ProductId, string ProductName, string? CategoryName, string UnitName, decimal Quantity, decimal SellingPrice, DateOnly? NearestExpiry);
