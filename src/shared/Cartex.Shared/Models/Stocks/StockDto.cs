namespace Cartex.Shared.Models.Stocks;

public record StockDto(long Id, long ProductId, string ProductName, string? CategoryName, string UnitName, decimal Quantity, decimal PurchasePrice, decimal SellingPrice, DateOnly? ExpiredAt);
