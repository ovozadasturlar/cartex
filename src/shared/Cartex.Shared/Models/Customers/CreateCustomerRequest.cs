namespace Cartex.Shared.Models.Customers;

public record CreateCustomerRequest(string FullName, string? Phone, string? CardBarcode, decimal DiscountPct, string? Email = null, string? LastName = null, string? Address = null, decimal? CreditLimit = null, bool NotificationsOptOut = false, decimal OpeningBalance = 0, string? OpeningCurrency = null, string? PreferredLanguage = null, long? AssignedUserId = null, double? Latitude = null, double? Longitude = null, long? AgentId = null, string? Note = null);
