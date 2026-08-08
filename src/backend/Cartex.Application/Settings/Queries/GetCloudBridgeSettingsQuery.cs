using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;

namespace Cartex.Application.Settings.Queries;

public record CloudBridgeSettingsDto(bool Enabled, string? GatewayUrl, bool HasLicenseKey);

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
