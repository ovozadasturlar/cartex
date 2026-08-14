namespace Cartex.Shared.Models.Sales;

public record SalesTotalsDto(int Count, decimal TotalAmount, decimal TotalDiscount, decimal TotalDebt);

public record DailySalesPointDto(DateTime Date, int Count, decimal TotalAmount);
