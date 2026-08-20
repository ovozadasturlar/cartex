using Cartex.Shared.Models.Settings;

namespace Cartex.Application.Common.Settings;

/// The stored settings and the wire shape are kept field-for-field identical, so the mapping
/// lives in one place instead of once per handler. `SalesPolicyContractTests` fails the build if
/// the two lists ever drift, which is the only way a new setting can silently stop being saved.
public static class SalesPolicyMapping
{
    public static SalesPolicyDto ToDto(SalesPolicySettings cfg) => new()
    {
        ShiftPolicy = cfg.ShiftPolicy,
        MaxDiscountPercent = cfg.MaxDiscountPercent,
        MaxDebtWriteOffAmount = cfg.MaxDebtWriteOffAmount,
        MaxDebtWriteOffPercent = cfg.MaxDebtWriteOffPercent,
        DefaultMinStock = cfg.DefaultMinStock,
        StaleRateDays = cfg.StaleRateDays,
        AllowDebtSales = cfg.AllowDebtSales,
        AllowCustomerCredit = cfg.AllowCustomerCredit,
        RequireDebtDueDate = cfg.RequireDebtDueDate,
        RequireSupplier = cfg.RequireSupplier,
        ShowOutOfStock = cfg.ShowOutOfStock,
        ShowUnlistedProducts = cfg.ShowUnlistedProducts,
        AllowInsufficientStockSales = cfg.AllowInsufficientStockSales,
        AllowNegativeStockWhenOffline = cfg.AllowNegativeStockWhenOffline,
        AllowRetroactiveCashback = cfg.AllowRetroactiveCashback,
        SaleCorrectionWindow = cfg.SaleCorrectionWindow,
        SaleCorrectionDays = cfg.SaleCorrectionDays,
        AllowDebtWriteOff = cfg.AllowDebtWriteOff,
        PrintMoneyDocuments = cfg.PrintMoneyDocuments,
        PrintCartProforma = cfg.PrintCartProforma,
        AllowConsolidatedAct = cfg.AllowConsolidatedAct,
        AllowCustomerLoans = cfg.AllowCustomerLoans,
        MaxCustomerLoan = cfg.MaxCustomerLoan,
        UpdateCatalogPriceOnSale = cfg.UpdateCatalogPriceOnSale,
        MaxPriceIncreasePercent = cfg.MaxPriceIncreasePercent,
        CustomerRequirement = cfg.CustomerRequirement,
        AllowReturnOnVoidedSale = cfg.AllowReturnOnVoidedSale,
        AllowFreeReturnLines = cfg.AllowFreeReturnLines,
        RequireReturnReason = cfg.RequireReturnReason,
        AllowSaleQueue = cfg.AllowSaleQueue
    };

    public static void Apply(SalesPolicySettings cfg, SalesPolicyDto dto)
    {
        cfg.ShiftPolicy = dto.ShiftPolicy;
        cfg.MaxDiscountPercent = dto.MaxDiscountPercent;
        cfg.MaxDebtWriteOffAmount = dto.MaxDebtWriteOffAmount;
        cfg.MaxDebtWriteOffPercent = dto.MaxDebtWriteOffPercent;
        cfg.DefaultMinStock = dto.DefaultMinStock;
        cfg.StaleRateDays = dto.StaleRateDays;
        cfg.AllowDebtSales = dto.AllowDebtSales;
        cfg.AllowCustomerCredit = dto.AllowCustomerCredit;
        cfg.RequireDebtDueDate = dto.RequireDebtDueDate;
        cfg.RequireSupplier = dto.RequireSupplier;
        cfg.ShowOutOfStock = dto.ShowOutOfStock;
        cfg.ShowUnlistedProducts = dto.ShowUnlistedProducts;
        cfg.AllowInsufficientStockSales = dto.AllowInsufficientStockSales;
        cfg.AllowNegativeStockWhenOffline = dto.AllowNegativeStockWhenOffline;
        cfg.AllowRetroactiveCashback = dto.AllowRetroactiveCashback;
        cfg.SaleCorrectionWindow = dto.SaleCorrectionWindow;
        cfg.SaleCorrectionDays = dto.SaleCorrectionDays;
        cfg.AllowDebtWriteOff = dto.AllowDebtWriteOff;
        cfg.PrintMoneyDocuments = dto.PrintMoneyDocuments;
        cfg.PrintCartProforma = dto.PrintCartProforma;
        cfg.AllowConsolidatedAct = dto.AllowConsolidatedAct;
        cfg.AllowCustomerLoans = dto.AllowCustomerLoans;
        cfg.MaxCustomerLoan = dto.MaxCustomerLoan;
        cfg.UpdateCatalogPriceOnSale = dto.UpdateCatalogPriceOnSale;
        cfg.MaxPriceIncreasePercent = dto.MaxPriceIncreasePercent;
        cfg.CustomerRequirement = dto.CustomerRequirement;
        cfg.AllowReturnOnVoidedSale = dto.AllowReturnOnVoidedSale;
        cfg.AllowFreeReturnLines = dto.AllowFreeReturnLines;
        cfg.RequireReturnReason = dto.RequireReturnReason;
        cfg.AllowSaleQueue = dto.AllowSaleQueue;
    }
}
