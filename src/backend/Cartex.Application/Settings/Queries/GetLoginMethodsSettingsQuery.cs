using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Shared.Models.Settings;

namespace Cartex.Application.Settings.Queries;

public record GetLoginMethodsSettingsQuery : IRequest<LoginMethodsSettingsDto>;

public sealed class GetLoginMethodsSettingsQueryHandler(ISettingsService settings) : IRequestHandler<GetLoginMethodsSettingsQuery, LoginMethodsSettingsDto>
{
    public async Task<LoginMethodsSettingsDto> Handle(GetLoginMethodsSettingsQuery request, CancellationToken cancellationToken)
    {
        var cfg = await settings.GetAsync<LoginMethodsSettings>(SettingKeys.LoginMethods, cancellationToken) ?? new();
        return new LoginMethodsSettingsDto(cfg.QrEnabled, cfg.QrRefreshSeconds, cfg.KeyEnabled);
    }
}
