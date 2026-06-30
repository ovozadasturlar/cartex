namespace Cartex.Shared.Models.Stocks;

public record StockOnHandPageDto(List<StockOnHandDto> Items, int TotalCount, decimal TotalQuantity, decimal TotalValue);
