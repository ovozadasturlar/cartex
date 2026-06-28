namespace Cartex.Shared.Models.Reports;

public record TopProductReportDto(long ProductId, string ProductName, decimal Quantity, decimal Revenue, decimal Profit);

public record DailySalesDto(DateTime Date, decimal Revenue, decimal Profit, int Count);

public record SalesReportDto(
    decimal Revenue,
    decimal Profit,
    int SalesCount,
    decimal AverageSale,
    decimal MaxSale,
    List<TopProductReportDto> TopProducts,
    List<DailySalesDto> Daily);
