using System.Text.Json;
using Cartex.Application.Common.Interfaces;
using Cartex.Domain.Authorization;
using Cartex.Domain.Entities;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Cartex.Infrastructure.Licensing;

public sealed class LicenseService(IApplicationDbContext db, IMemoryCache cache) : ILicenseService
{
    private const string CacheKey = "license:status";
    private const string FeaturesCacheKey = "license:features";

    public async Task<LicenseStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue<LicenseStatus>(CacheKey, out var cached) && cached is not null)
            return cached;

        var license = await db.LicenseStates.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        var enabled = Parse(license?.EnabledFeatures);
        var status = license is null
            ? new LicenseStatus(false, TariffCatalog.Free, null, [])
            : new LicenseStatus(
                license.ExpiresAt is null || license.ExpiresAt > DateTime.UtcNow,
                license.Tariff,
                license.ExpiresAt,
                enabled is null ? [] : FeatureCatalog.Normalize(enabled).ToList());

        cache.Set(CacheKey, status, TimeSpan.FromSeconds(60));
        return status;
    }

    public async Task<bool> IsActiveAsync(CancellationToken cancellationToken = default)
        => (await GetStatusAsync(cancellationToken)).IsActive;

    public async Task<IReadOnlySet<string>> GetTariffFeaturesAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue<IReadOnlySet<string>>(FeaturesCacheKey, out var cached) && cached is not null)
            return cached;

        var license = await db.LicenseStates.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        var features = Resolve(license);

        cache.Set(FeaturesCacheKey, features, TimeSpan.FromSeconds(60));
        return features;
    }

    public void Invalidate()
    {
        cache.Remove(CacheKey);
        cache.Remove(FeaturesCacheKey);
    }

    private static IReadOnlySet<string> Resolve(LicenseState? license)
    {
        if (license is null) return TariffCatalog.FeaturesFor(TariffCatalog.Free);
        var configured = Parse(license.EnabledFeatures);
        return configured is null
            ? TariffCatalog.FeaturesFor(license.Tariff)
            : FeatureCatalog.Normalize(configured);
    }

    private static IReadOnlySet<string>? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var codes = JsonSerializer.Deserialize<List<string>>(json);
            return codes is { Count: > 0 } ? codes.ToHashSet() : null;
        }
        catch
        {
            return null;
        }
    }
}
