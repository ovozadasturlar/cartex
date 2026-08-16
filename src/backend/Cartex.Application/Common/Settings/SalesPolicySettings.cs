namespace Cartex.Application.Common.Settings;

public sealed class SalesPolicySettings
{
    public string ShiftPolicy { get; set; } = "CashOnly";
    public decimal MaxDiscountPercent { get; set; }

    /// Yaxlitlashda kechiriladigan eng katta summa. 0 — chegara yo'q (MaxDiscountPercent bilan bir xil shart).
    public decimal MaxRoundingAmount { get; set; }

    /// Bir hujjatda kechirilishi mumkin bo'lgan eng katta qarz. 0 — chegara yo'q.
    public decimal MaxDebtWriteOffAmount { get; set; }

    /// Kechirim shu hujjat yopayotgan summaning (to'langan + kechirilgan) necha foizidan
    /// oshmasligi kerak. 0 — chegara yo'q.
    public decimal MaxDebtWriteOffPercent { get; set; }
    public decimal DefaultMinStock { get; set; }
    public int StaleRateDays { get; set; } = 3;
    public bool AllowDebtSales { get; set; } = true;
    public bool AllowCustomerCredit { get; set; }
    public bool RequireDebtDueDate { get; set; } = true;
    public bool RequireSupplier { get; set; }
    public bool ShowOutOfStock { get; set; }
    public bool ShowUnlistedProducts { get; set; } = true;
    public bool AllowInsufficientStockSales { get; set; }
    public bool AllowRetroactiveCashback { get; set; }
    public string SaleCorrectionWindow { get; set; } = "Shift";
    public int SaleCorrectionDays { get; set; } = 1;
}
