using Cartex.Application.Common.Interfaces;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Cartex.Infrastructure.Features;

public sealed class FeatureStateProvider(IApplicationDbContext db, ILicenseService license, IMemoryCache cache) : IFeatureStateProvider
{
    private const string CacheKey = "feature-states";

    public async Task<bool> IsEnabledAsync(string code, CancellationToken cancellationToken = default)
    {
        var states = await cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
            return await db.Features.ToDictionaryAsync(f => f.Code, f => f.IsEnabled && f.OwnerEnabled, cancellationToken);
        });

        if (states is not null && states.TryGetValue(code, out var enabled) && !enabled)
            return false;

        var permitted = await license.GetTariffFeaturesAsync(cancellationToken);
        if (!permitted.Contains(code))
            return false;

        if (!await license.IsActiveAsync(cancellationToken))
            return TariffCatalog.FeaturesFor(TariffCatalog.Free).Contains(code);

        return true;
    }

    public void Invalidate() => cache.Remove(CacheKey);
}
