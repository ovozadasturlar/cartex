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

    /// Savdoda katalogdan yuqori narx kiritilsa, katalog narxi yangilanadimi (NARX-06).
    public bool UpdateCatalogPriceOnSale { get; set; } = true;

    /// Ixtiyoriy imkoniyatlarning kalitlari (SOZ-08). Chegara maydoni o'chirish vositasi emas:
    /// unda 0 — "chegara yo'q" degani, shuning uchun har biriga alohida kalit kerak (SOZ-09).
    public bool AllowRounding { get; set; } = true;
    public bool AllowDebtWriteOff { get; set; } = true;
    public bool PrintMoneyDocuments { get; set; } = true;
    public bool AllowConsolidatedAct { get; set; } = true;

    /// Do'kon mijozga savdosiz naqd qarz bera oladimi (QARZ-09). Standart: yo'q —
    /// bu kassadan pul chiqaradigan alohida imkoniyat, ataylab yopiq turadi.
    public bool AllowCustomerLoans { get; set; }

    /// Bitta chiqimda qarzga berilishi mumkin bo'lgan eng katta summa. 0 — chegara yo'q.
    public decimal MaxCustomerLoan { get; set; }

    /// Katalogni yangilash uchun ruxsat etilgan eng katta oshish, foizda (NARX-07).
    /// Oshirish chegaradan katta bo'lsa savdo baribir o'tadi, faqat katalog yangilanmaydi.
    /// 0 — chegara yo'q.
    public decimal MaxPriceIncreasePercent { get; set; }
}
