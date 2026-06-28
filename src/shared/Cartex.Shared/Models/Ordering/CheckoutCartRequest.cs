namespace Cartex.Shared.Models.Ordering;

public record CheckoutCartRequest(decimal PaidCash, decimal PaidCard, decimal PaidBonus);
