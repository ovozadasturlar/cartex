namespace Cartex.Shared.Models.Reports;

public record CashierSalesDto(long UserId, string UserName, decimal Revenue, int Count);

public record CategorySalesDto(string? CategoryName, decimal Quantity, decimal Revenue);

public record SalesBreakdownReportDto(
    decimal Cash,
    decimal Card,
    decimal Bonus,
    decimal Debt,
    List<CashierSalesDto> ByCashier,
    List<CategorySalesDto> ByCategory,
    decimal Credit = 0);
