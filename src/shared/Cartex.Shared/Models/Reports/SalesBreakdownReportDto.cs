namespace Cartex.Shared.Models.Reports;

public record CashierSalesDto(long UserId, string UserName, decimal Revenue, int Count);

public record CategorySalesDto(string? CategoryName, decimal Quantity, decimal Revenue);

/// HIS-04: `Cash + Card + Bonus + Advance + Debt − Credit − Returned` savdo hisobotidagi
/// daromadga aynan teng bo'ladi. Ustunlardan biri tushib qolsa yoki qaytarilgan qism
/// ko'rsatilmasa, egasi yonma-yon turgan ikki kartaning farqini tushuntira olmaydi.
public record SalesBreakdownReportDto(
    decimal Cash,
    decimal Card,
    decimal Bonus,
    decimal Debt,
    List<CashierSalesDto> ByCashier,
    List<CategorySalesDto> ByCategory,
    decimal Credit = 0,
    decimal Advance = 0,
    decimal Returned = 0);
