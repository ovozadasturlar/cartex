namespace Cartex.Shared.Models.Reports;

public record CustomerSalesDto(long CustomerId, string CustomerName, decimal Revenue, decimal Profit, int SalesCount, DateTime LastPurchase);
