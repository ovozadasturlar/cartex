namespace Cartex.Shared.Models.Customers;

public record RepayDebtRequest(decimal Amount, bool ViaCard, string? DebtCurrency = null, string? PayCurrency = null, string? IdempotencyKey = null, decimal WriteOff = 0, string? WriteOffReason = null);
