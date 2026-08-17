using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Shared.Models.Settings;

namespace Cartex.Application.Settings.Queries;

public record GetCloudBridgeSettingsQuery : IRequest<CloudBridgeSettingsDto>;

public sealed class GetCloudBridgeSettingsQueryHandler(ISettingsService settings)
    : IRequestHandler<GetCloudBridgeSettingsQuery, CloudBridgeSettingsDto>
{
    public async Task<CloudBridgeSettingsDto> Handle(GetCloudBridgeSettingsQuery request, CancellationToken cancellationToken)
    {
        var cfg = await settings.GetAsync<CloudBridgeSettings>(SettingKeys.CloudBridge, cancellationToken) ?? new CloudBridgeSettings();
        return new CloudBridgeSettingsDto(cfg.Enabled, cfg.GatewayUrl, !string.IsNullOrEmpty(cfg.LicenseKey));
    }
}
