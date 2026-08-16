using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Persistence;
using Cartex.Shared.Models.Settings;
using FluentValidation;

namespace Cartex.Application.Settings.Commands;

public sealed record UpdateSalesPolicyCommand(SalesPolicyDto Policy) : ICommand<Unit>;

public sealed class UpdateSalesPolicyCommandHandler(ISettingsService settings, IAuditService audit)
    : IRequestHandler<UpdateSalesPolicyCommand, Unit>
{
    public async Task<Unit> Handle(UpdateSalesPolicyCommand request, CancellationToken cancellationToken)
    {
        var cfg = await settings.GetAsync<SalesPolicySettings>(SettingKeys.SalesPolicy, cancellationToken) ?? new SalesPolicySettings();
        SalesPolicyMapping.Apply(cfg, request.Policy);
        await settings.SetAsync(SettingKeys.SalesPolicy, cfg, cancellationToken);
        audit.Add("settings", "settings", null, new { section = "salesPolicy" });
        return Unit.Value;
    }
}

public sealed class UpdateSalesPolicyCommandValidator : AbstractValidator<UpdateSalesPolicyCommand>
{
    public UpdateSalesPolicyCommandValidator()
    {
        RuleFor(x => x.Policy.ShiftPolicy).Must(p => p is "Off" or "CashOnly" or "AllSales");
        RuleFor(x => x.Policy.MaxDiscountPercent).InclusiveBetween(0, 100);
        RuleFor(x => x.Policy.MaxRoundingAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Policy.MaxDebtWriteOffAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Policy.MaxDebtWriteOffPercent).InclusiveBetween(0, 100);
        RuleFor(x => x.Policy.MaxPriceIncreasePercent).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Policy.DefaultMinStock).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Policy.StaleRateDays).InclusiveBetween(1, 30);
    }
}
