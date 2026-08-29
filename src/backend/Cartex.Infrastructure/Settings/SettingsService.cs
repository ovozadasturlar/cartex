using System.Text.Json;
using Cartex.Application.Common.Interfaces;
using Cartex.Domain.Entities;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Cartex.Infrastructure.Settings;

public sealed class SettingsService(IApplicationDbContext db, IMemoryCache cache) : ISettingsService
{
    private static string CacheKey(string key) => $"setting:{key}";

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(CacheKey(key), out string? json))
            return json is null ? default : JsonSerializer.Deserialize<T>(json);

        json = await db.BusinessSettings.AsNoTracking()
            .Where(s => s.Key == key)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken);

        // Yo'q qiymat keshlanmaydi: aks holda endigina yozilgan sozlama 60 soniya davomida
        // "yo'q" bo'lib ko'rinib, chaqiruvchi uni ikkinchi marta yozib yuborardi.
        if (json is not null) cache.Set(CacheKey(key), json, TimeSpan.FromSeconds(60));
        return json is null ? default : JsonSerializer.Deserialize<T>(json);
    }

    public async Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(value);
        var setting = await db.BusinessSettings.FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
        if (setting is null)
            db.BusinessSettings.Add(new BusinessSetting { Key = key, Value = json });
        else
            setting.Value = json;
        await db.SaveChangesAsync(cancellationToken);
        db.RunAfterCommit(() => cache.Remove(CacheKey(key)));
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        var setting = await db.BusinessSettings.FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
        if (setting is null) return;
        db.BusinessSettings.Remove(setting);
        await db.SaveChangesAsync(cancellationToken);
        db.RunAfterCommit(() => cache.Remove(CacheKey(key)));
    }
}
