namespace Cartex.Shared.Models.Stocks;

public record StockDto(long Id, string ProductName, string? CategoryName, string UnitName, decimal Quantity, decimal PurchasePrice, decimal SellingPrice, DateOnly? ExpiredAt);
