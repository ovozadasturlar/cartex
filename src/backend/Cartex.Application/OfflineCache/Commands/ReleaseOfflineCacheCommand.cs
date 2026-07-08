using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Persistence;

using Unit = Cartex.Application.Common.Messaging.Unit;

namespace Cartex.Application.OfflineCache.Commands;

public record ReleaseOfflineCacheCommand : ICommand<Unit>;

public sealed class ReleaseOfflineCacheCommandHandler(ISettingsService settings, IAuditService audit)
    : IRequestHandler<ReleaseOfflineCacheCommand, Unit>
{
    public async Task<Unit> Handle(ReleaseOfflineCacheCommand request, CancellationToken cancellationToken)
    {
        var cfg = await settings.GetAsync<OfflineCacheSettings>(SettingKeys.OfflineCache, cancellationToken) ?? new OfflineCacheSettings();
        audit.Add("offlineRelease", "settings", null, new { cfg.DeviceId, cfg.DeviceName });
        await settings.SetAsync(SettingKeys.OfflineCache, new OfflineCacheSettings(), cancellationToken);
        return Unit.Value;
    }
}
