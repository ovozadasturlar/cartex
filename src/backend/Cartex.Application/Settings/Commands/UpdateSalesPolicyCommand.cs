using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Persistence;
using FluentValidation;

namespace Cartex.Application.Settings.Commands;

public record UpdateSalesPolicyCommand(string ShiftPolicy, decimal MaxDiscountPercent, decimal DefaultMinStock, int StaleRateDays) : ICommand<Unit>;

public sealed class UpdateSalesPolicyCommandHandler(ISettingsService settings, IAuditService audit)
    : IRequestHandler<UpdateSalesPolicyCommand, Unit>
{
    public async Task<Unit> Handle(UpdateSalesPolicyCommand request, CancellationToken cancellationToken)
    {
        var cfg = new SalesPolicySettings
        {
            ShiftPolicy = request.ShiftPolicy,
            MaxDiscountPercent = request.MaxDiscountPercent,
            DefaultMinStock = request.DefaultMinStock,
            StaleRateDays = request.StaleRateDays
        };
        audit.Add("settings", "settings", null, new { section = "salesPolicy" });
        await settings.SetAsync(SettingKeys.SalesPolicy, cfg, cancellationToken);
        return Unit.Value;
    }
}

public sealed class UpdateSalesPolicyCommandValidator : AbstractValidator<UpdateSalesPolicyCommand>
{
    public UpdateSalesPolicyCommandValidator()
    {
        RuleFor(x => x.ShiftPolicy).Must(p => p is "Off" or "CashOnly" or "AllSales");
        RuleFor(x => x.MaxDiscountPercent).InclusiveBetween(0, 100);
        RuleFor(x => x.DefaultMinStock).GreaterThanOrEqualTo(0);
        RuleFor(x => x.StaleRateDays).InclusiveBetween(1, 30);
    }
}
