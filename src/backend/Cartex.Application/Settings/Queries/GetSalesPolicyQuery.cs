using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;

namespace Cartex.Application.Settings.Queries;

public record SalesPolicyDto(string ShiftPolicy, decimal MaxDiscountPercent, decimal DefaultMinStock, int StaleRateDays, bool AllowDebtSales = true, bool AllowCustomerCredit = false, bool RequireDebtDueDate = true, bool RequireSupplier = false, bool ShowOutOfStock = false);

public record GetSalesPolicyQuery : IRequest<SalesPolicyDto>;

public sealed class GetSalesPolicyQueryHandler(ISettingsService settings)
    : IRequestHandler<GetSalesPolicyQuery, SalesPolicyDto>
{
    public async Task<SalesPolicyDto> Handle(GetSalesPolicyQuery request, CancellationToken cancellationToken)
    {
        var cfg = await settings.GetAsync<SalesPolicySettings>(SettingKeys.SalesPolicy, cancellationToken) ?? new SalesPolicySettings();
        return new SalesPolicyDto(cfg.ShiftPolicy, cfg.MaxDiscountPercent, cfg.DefaultMinStock, cfg.StaleRateDays, cfg.AllowDebtSales, cfg.AllowCustomerCredit, cfg.RequireDebtDueDate, cfg.RequireSupplier, cfg.ShowOutOfStock);
    }
}
