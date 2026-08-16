using Cartex.Shared.Models.Sales;

namespace Cartex.Shared.Models.Ordering;

public record CheckoutCartItemDto(long VariantId, decimal Quantity, decimal? UnitPrice = null);

public record CheckoutCartRequest(
    decimal PaidCash,
    decimal PaidCard,
    decimal PaidBonus,
    string? IdempotencyKey = null,
    List<CheckoutCartItemDto>? Items = null,
    List<SalePaymentRequest>? Payments = null,
    string? DebtCurrency = null,
    DateOnly? DebtDueDate = null,
    decimal? CreditAmount = null,
    bool? UseCustomerAdvance = null,
    long? CustomerId = null,
    decimal? DiscountAmount = null,
    string? Note = null,
    decimal? RoundingAmount = null);
