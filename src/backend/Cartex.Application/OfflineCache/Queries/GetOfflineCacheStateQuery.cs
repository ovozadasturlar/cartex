using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;

namespace Cartex.Application.OfflineCache.Queries;

public record OfflineCacheStateDto(string? DeviceId, string? DeviceName, DateTime? ClaimedAt);

public record GetOfflineCacheStateQuery : IRequest<OfflineCacheStateDto>;

public sealed class GetOfflineCacheStateQueryHandler(ISettingsService settings)
    : IRequestHandler<GetOfflineCacheStateQuery, OfflineCacheStateDto>
{
    public async Task<OfflineCacheStateDto> Handle(GetOfflineCacheStateQuery request, CancellationToken cancellationToken)
    {
        var cfg = await settings.GetAsync<OfflineCacheSettings>(SettingKeys.OfflineCache, cancellationToken) ?? new OfflineCacheSettings();
        return new OfflineCacheStateDto(cfg.DeviceId, cfg.DeviceName, cfg.ClaimedAt);
    }
}
