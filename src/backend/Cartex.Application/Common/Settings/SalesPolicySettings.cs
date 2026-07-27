namespace Cartex.Application.Common.Settings;

public sealed class SalesPolicySettings
{
    public string ShiftPolicy { get; set; } = "CashOnly";
    public decimal MaxDiscountPercent { get; set; }
    public decimal DefaultMinStock { get; set; }
    public int StaleRateDays { get; set; } = 3;
    public bool AllowDebtSales { get; set; } = true;
    public bool AllowCustomerCredit { get; set; }
    public bool RequireDebtDueDate { get; set; } = true;
    public bool RequireSupplier { get; set; }
    public bool ShowOutOfStock { get; set; }
    public bool ShowUnlistedProducts { get; set; } = true;
    public bool AllowInsufficientStockSales { get; set; }
}
