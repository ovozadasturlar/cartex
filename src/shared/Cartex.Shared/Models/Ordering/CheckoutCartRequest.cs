namespace Cartex.Shared.Models.Ordering;

public record CheckoutCartItemDto(long VariantId, decimal Quantity, decimal? UnitPrice = null);

public record CheckoutCartRequest(
    decimal PaidCash,
    decimal PaidCard,
    decimal PaidBonus,
    string? IdempotencyKey = null,
    List<CheckoutCartItemDto>? Items = null);
