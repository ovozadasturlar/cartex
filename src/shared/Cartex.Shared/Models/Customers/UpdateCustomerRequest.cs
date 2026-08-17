namespace Cartex.Shared.Models.Customers;

public record UpdateCustomerRequest(string FullName, string? Phone, string? CardBarcode, decimal DiscountPct, string? Email = null, string? LastName = null, string? Address = null, decimal? CreditLimit = null, bool NotificationsOptOut = false, string? PreferredLanguage = null, string? Note = null);
