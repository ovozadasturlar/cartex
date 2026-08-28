namespace Cartex.Shared.Models.Stocks;

public record OpeningStockBackfillDto(int Movements, decimal Quantity, int AlreadyReconciled);
