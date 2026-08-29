namespace Cartex.Application.Common.Settings;

public sealed class SalesPolicySettings
{
    public string ShiftPolicy { get; set; } = "CashOnly";
    /// Bo'sh — chegara yo'q, 0 — chegirma umuman berilmaydi (SOZ-02).
    public decimal? MaxDiscountPercent { get; set; }


    /// Bir hujjatda kechirilishi mumkin bo'lgan eng katta qarz. Bo'sh — chegara yo'q,
    /// 0 — kechirim yopiq (SOZ-02).
    public decimal? MaxDebtWriteOffAmount { get; set; }

    /// Kechirim shu hujjat yopayotgan summaning (to'langan + kechirilgan) necha foizidan
    /// oshmasligi kerak. Bo'sh — chegara yo'q, 0 — kechirim yopiq (SOZ-02).
    public decimal? MaxDebtWriteOffPercent { get; set; }
    public decimal DefaultMinStock { get; set; }
    public int StaleRateDays { get; set; } = 3;
    public bool AllowDebtSales { get; set; } = true;
    public bool AllowCustomerCredit { get; set; }
    public bool RequireDebtDueDate { get; set; } = true;
    public bool RequireSupplier { get; set; }
    public bool ShowOutOfStock { get; set; }
    public bool ShowUnlistedProducts { get; set; } = true;
    public bool AllowInsufficientStockSales { get; set; }

    /// Qoldiq nazorati faqat oflayn oynada — vakolat egasi jim bo'lgan paytda va faqat vakolat
    /// omborida — yumshatiladi (OFF-16). `AllowInsufficientStockSales` dan farqi shu: u har doim
    /// va hamma yerda ochiq.
    public bool AllowNegativeStockWhenOffline { get; set; }
    public bool AllowRetroactiveCashback { get; set; }
    public string SaleCorrectionWindow { get; set; } = "Shift";
    public int SaleCorrectionDays { get; set; } = 1;

    /// Savdoda katalogdan yuqori narx kiritilsa, katalog narxi yangilanadimi (NARX-06).
    /// NARX-10: kassir ko'rgan narx shu oyna ichida katalogda turgan bo'lsa savdo o'sha narxda
    /// o'tadi. 0 — tarixdan qidirilmaydi, ya'ni har qanday farq savdoni to'xtatadi.
    public int PriceDriftWindowMinutes { get; set; } = 60;

    public bool UpdateCatalogPriceOnSale { get; set; } = true;

    /// Ixtiyoriy imkoniyatlarning kalitlari (SOZ-08). Chegara maydoni imkoniyatni butunlay
    /// yopish uchun ishlatilmaydi — buning uchun alohida kalit bor (SOZ-09).
    public bool AllowDebtWriteOff { get; set; } = true;
    public bool PrintMoneyDocuments { get; set; } = true;

    /// Savat proformasini ("oldindan chop etish") mijozga berish do'kon ishida bormi
    /// (SOZ-14). Qaysi printer chiqarishi — bu emas, chop etish bo'limining ishi.
    public bool PrintCartProforma { get; set; } = true;
    public bool AllowConsolidatedAct { get; set; } = true;

    /// Do'kon mijozga savdosiz naqd qarz bera oladimi (QARZ-09). Standart: yo'q —
    /// bu kassadan pul chiqaradigan alohida imkoniyat, ataylab yopiq turadi.
    public bool AllowCustomerLoans { get; set; }

    /// Bitta chiqimda qarzga berilishi mumkin bo'lgan eng katta summa. Bo'sh — chegara yo'q,
    /// 0 — qarzga berish yopiq (SOZ-02).
    public decimal? MaxCustomerLoan { get; set; }

    /// Katalogni yangilash uchun ruxsat etilgan eng katta oshish, foizda (NARX-07).
    /// Oshirish chegaradan katta bo'lsa savdo baribir o'tadi, faqat katalog yangilanmaydi.
    /// Bo'sh — chegara yo'q, 0 — katalog savdodan yangilanmaydi (SOZ-02).
    public decimal? MaxPriceIncreasePercent { get; set; }

    /// Savdoda mijoz qachon majburiy (SOZ-11): "Optional", "OnDebt" (standart), "Always".
    public string CustomerRequirement { get; set; } = "OnDebt";

    /// Bekor qilingan savdoga qaytarish rasmiylashtirsa bo'ladimi (QAYT-08). Standart: yo'q —
    /// bunday savdo allaqachon ortga qaytarilgan, ustiga qaytarish tovarni ikki marta kirim
    /// qilib pulni ikki marta chiqaradi. Do'kon o'z siyosati bilan ocha oladi.
    public bool AllowReturnOnVoidedSale { get; set; }

    /// Savdoga bog'lanmagan erkin qator — sotilganidan ortiq miqdor ham shu yo'l bilan
    /// ketadi — qabul qilinadimi (QAYT-09). Ruxsat xodimni, bu kalit do'konni boshqaradi.
    public bool AllowFreeReturnLines { get; set; } = true;

    /// Har qaytarish qatorida sabab majburiymi (QAYT-10).
    public bool RequireReturnReason { get; set; }

    /// Mijoz kredit limitining qattiqligi (QARZ-22): "Block" (standart) — limitdan oshiradigan
    /// qarz rad etiladi, "Warn" — savdo o'tadi va javobda ogohlantirish qaytadi. Taqiqni ochmaydi:
    /// nol limit va o'chirilgan nasiya savdo har ikki rejimda ham rad etiladi.
    public string CreditLimitEnforcement { get; set; } = "Block";

    /// Yangi mijoz formasida oldindan turadigan qarz limiti (SOZ-17). Bo'sh — forma bo'sh
    /// ochiladi va limit cheklanmagan bo'ladi. Server buni o'zi qo'llamaydi: bo'sh kelgan limit
    /// cheklanmagan bo'lib saqlanadi, aks holda "cheklanmagan" ni ifodalashning iloji qolmasdi.
    public decimal? DefaultCreditLimit { get; set; }

    public bool TrackWriteOff { get; set; }

    /// Navbat ish uslubi yoqilganmi (NAVBAT-06). Bu tarif moduli emas — bir do'kon hamma
    /// narsani bitta kassada uradi, boshqasida yig'uvchi tayyorlab kassir pul oladi.
    public bool AllowSaleQueue { get; set; } = true;
}
