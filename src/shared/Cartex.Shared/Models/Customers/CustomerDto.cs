namespace Cartex.Shared.Models.Customers;

public record CustomerDto(long Id, string FullName, string? Phone, string? CardBarcode, decimal DiscountPct, decimal CashbackBalance, decimal DebtBalance);
