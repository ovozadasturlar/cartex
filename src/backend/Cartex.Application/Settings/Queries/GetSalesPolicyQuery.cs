using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;

namespace Cartex.Application.Settings.Queries;

public record SalesPolicyDto(string ShiftPolicy, decimal MaxDiscountPercent, decimal DefaultMinStock, int StaleRateDays, bool AllowDebtSales = true, bool AllowCustomerCredit = false, bool RequireDebtDueDate = true, bool RequireSupplier = false, bool ShowOutOfStock = false, bool ShowUnlistedProducts = true, bool AllowInsufficientStockSales = false, bool AllowRetroactiveCashback = false, string SaleCorrectionWindow = "Shift", int SaleCorrectionDays = 1, decimal MaxRoundingAmount = 0, decimal MaxDebtWriteOffAmount = 0, decimal MaxDebtWriteOffPercent = 0);

public record GetSalesPolicyQuery : IRequest<SalesPolicyDto>;

public sealed class GetSalesPolicyQueryHandler(ISettingsService settings)
    : IRequestHandler<GetSalesPolicyQuery, SalesPolicyDto>
{
    public async Task<SalesPolicyDto> Handle(GetSalesPolicyQuery request, CancellationToken cancellationToken)
    {
        var cfg = await settings.GetAsync<SalesPolicySettings>(SettingKeys.SalesPolicy, cancellationToken) ?? new SalesPolicySettings();
        return new SalesPolicyDto(cfg.ShiftPolicy, cfg.MaxDiscountPercent, cfg.DefaultMinStock, cfg.StaleRateDays, cfg.AllowDebtSales, cfg.AllowCustomerCredit, cfg.RequireDebtDueDate, cfg.RequireSupplier, cfg.ShowOutOfStock, cfg.ShowUnlistedProducts, cfg.AllowInsufficientStockSales, cfg.AllowRetroactiveCashback, cfg.SaleCorrectionWindow, cfg.SaleCorrectionDays, cfg.MaxRoundingAmount,
            cfg.MaxDebtWriteOffAmount, cfg.MaxDebtWriteOffPercent);
    }
}
