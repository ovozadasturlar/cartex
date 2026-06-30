namespace Cartex.Shared.Models.Sales;

public record ReturnLineRequest(long SaleItemId, decimal Quantity, bool Restock, string? Reason);

public record ReturnSaleRequest(long SaleId, List<ReturnLineRequest> Lines);
