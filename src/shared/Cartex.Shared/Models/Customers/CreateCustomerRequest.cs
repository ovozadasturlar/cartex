namespace Cartex.Shared.Models.Customers;

public record CreateCustomerRequest(string FullName, string? Phone, string? CardBarcode, decimal DiscountPct, string? Email = null, string? LastName = null, string? Address = null, decimal CreditLimit = 0, bool NotificationsOptOut = false, decimal OpeningBalance = 0, string? OpeningCurrency = null, string? PreferredLanguage = null);
