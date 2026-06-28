namespace Cartex.Shared.Models.Stocks;

public record ExpiringStockDto(long Id, string ProductName, string WarehouseName, decimal Quantity, DateOnly ExpiredAt);
