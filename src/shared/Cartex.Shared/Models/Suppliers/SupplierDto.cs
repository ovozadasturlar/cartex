namespace Cartex.Shared.Models.Suppliers;

public record SupplierDto(long Id, string Name, string? Phone, decimal Payable);

public record PaySupplierDebtRequest(decimal Amount, string Method = "Cash", string? DebtCurrency = null, string? PayCurrency = null, long? SupplyId = null);
