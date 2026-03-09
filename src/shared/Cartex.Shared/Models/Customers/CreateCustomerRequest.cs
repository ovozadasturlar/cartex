namespace Cartex.Shared.Models.Customers;

public record CreateCustomerRequest(string FullName, string? Phone, string? CardBarcode, decimal DiscountPct);
