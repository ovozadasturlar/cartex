using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Domain.Enums;

namespace Cartex.Application.Settings.Queries;

public sealed record TradeCaseSettingsDto(
    bool Enabled,
    string SingularLabel,
    string PluralLabel,
    TradeCaseWorkflow DefaultWorkflow,
    TradeCasePricePolicy DefaultPricePolicy,
    bool AllowWorkflowOverride,
    bool AllowPricePolicyOverride,
    bool RequireSiteAddress,
    bool AutoUseCustomerAdvance);

public sealed record GetTradeCaseSettingsQuery : IRequest<TradeCaseSettingsDto>;

public sealed class GetTradeCaseSettingsQueryHandler(ISettingsService settings)
    : IRequestHandler<GetTradeCaseSettingsQuery, TradeCaseSettingsDto>
{
    public async Task<TradeCaseSettingsDto> Handle(GetTradeCaseSettingsQuery request, CancellationToken cancellationToken)
    {
        var value = await settings.GetAsync<TradeCaseSettings>(SettingKeys.TradeCases, cancellationToken)
                    ?? new TradeCaseSettings();
        return new TradeCaseSettingsDto(value.Enabled, value.SingularLabel, value.PluralLabel,
            value.DefaultWorkflow, value.DefaultPricePolicy, value.AllowWorkflowOverride,
            value.AllowPricePolicyOverride, value.RequireSiteAddress, value.AutoUseCustomerAdvance);
    }
}
