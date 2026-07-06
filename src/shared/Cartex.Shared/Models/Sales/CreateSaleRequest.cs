namespace Cartex.Shared.Models.Sales;

public record CreateSaleItemRequest(long VariantId, decimal Quantity, decimal? UnitPrice = null, long? UnitId = null);

public record SalePaymentRequest(string Method, string Currency, decimal Amount);

public record CreateSaleRequest(
    long WarehouseId,
    long? CustomerId,
    decimal PaidCash,
    decimal PaidCard,
    decimal PaidBonus,
    List<CreateSaleItemRequest> Items,
    decimal DiscountAmount = 0,
    List<SalePaymentRequest>? Payments = null,
    string? DebtCurrency = null,
    DateOnly? DebtDueDate = null);
