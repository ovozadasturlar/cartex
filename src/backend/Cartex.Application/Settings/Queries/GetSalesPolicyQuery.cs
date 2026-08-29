using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Shared.Models.Settings;

namespace Cartex.Application.Settings.Queries;

public record GetSalesPolicyQuery : IRequest<SalesPolicyDto>;

public sealed class GetSalesPolicyQueryHandler(ISettingsService settings)
    : IRequestHandler<GetSalesPolicyQuery, SalesPolicyDto>
{
    public async Task<SalesPolicyDto> Handle(GetSalesPolicyQuery request, CancellationToken cancellationToken)
    {
        var cfg = await settings.GetAsync<SalesPolicySettings>(SettingKeys.SalesPolicy, cancellationToken) ?? new SalesPolicySettings();
        return SalesPolicyMapping.ToDto(cfg);
    }
}
