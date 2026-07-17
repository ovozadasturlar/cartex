namespace Cartex.Shared.Models.Suppliers;

public record SupplierDto(long Id, string Name, string? Phone, decimal Payable);

public record PaySupplierDebtRequest(decimal Amount, string Method = "Cash", string? DebtCurrency = null, string? PayCurrency = null, long? SupplyId = null);

public record SupplierPaymentDto(long TransactionId, DateTime CreatedAt, decimal Amount, string Currency, string Method, string? UserName);
