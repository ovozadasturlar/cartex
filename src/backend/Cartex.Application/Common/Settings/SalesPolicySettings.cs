namespace Cartex.Application.Common.Settings;

public sealed class SalesPolicySettings
{
    public string ShiftPolicy { get; set; } = "CashOnly";
    public decimal MaxDiscountPercent { get; set; }
    public decimal DefaultMinStock { get; set; }
    public int StaleRateDays { get; set; } = 3;
}
