namespace Cartex.Shared.Models.Suppliers;

public record SupplierDto(long Id, string Name, string? Phone, decimal Payable);

public record PaySupplierDebtRequest(decimal Amount, bool ViaCard, string? DebtCurrency = null, string? PayCurrency = null);
