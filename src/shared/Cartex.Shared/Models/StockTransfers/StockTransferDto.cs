namespace Cartex.Shared.Models.StockTransfers;

public record StockTransferDto(
    long Id,
    string ProductName,
    decimal Quantity,
    string FromWarehouse,
    string ToWarehouse,
    string Status,
    DateTime CreatedAt,
    string UserName);
