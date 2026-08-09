using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using FluentValidation;

namespace Cartex.Application.Settings.Commands;

public sealed record UpdateTradeCaseSettingsCommand(
    bool Enabled,
    string SingularLabel,
    string PluralLabel,
    TradeCaseWorkflow DefaultWorkflow,
    TradeCasePricePolicy DefaultPricePolicy,
    bool AllowWorkflowOverride = true,
    bool AllowPricePolicyOverride = true,
    bool RequireSiteAddress = false,
    bool AutoUseCustomerAdvance = true) : ICommand<Unit>;

public sealed class UpdateTradeCaseSettingsCommandHandler(ISettingsService settings, IAuditService audit)
    : IRequestHandler<UpdateTradeCaseSettingsCommand, Unit>
{
    public async Task<Unit> Handle(UpdateTradeCaseSettingsCommand request, CancellationToken cancellationToken)
    {
        var value = new TradeCaseSettings
        {
            Enabled = request.Enabled,
            SingularLabel = request.SingularLabel.Trim(),
            PluralLabel = request.PluralLabel.Trim(),
            DefaultWorkflow = request.DefaultWorkflow,
            DefaultPricePolicy = request.DefaultPricePolicy,
            AllowWorkflowOverride = request.AllowWorkflowOverride,
            AllowPricePolicyOverride = request.AllowPricePolicyOverride,
            RequireSiteAddress = request.RequireSiteAddress,
            AutoUseCustomerAdvance = request.AutoUseCustomerAdvance
        };
        await settings.SetAsync(SettingKeys.TradeCases, value, cancellationToken);
        audit.SetOutcome("settings.trade_cases_updated", "settings", null, value,
            "Loyiha jarayoni sozlamalari yangilandi");
        return Unit.Value;
    }
}

public sealed class UpdateTradeCaseSettingsCommandValidator : AbstractValidator<UpdateTradeCaseSettingsCommand>
{
    public UpdateTradeCaseSettingsCommandValidator()
    {
        RuleFor(x => x.SingularLabel).NotEmpty().MaximumLength(40);
        RuleFor(x => x.PluralLabel).NotEmpty().MaximumLength(60);
    }
}
