using System.Text.Json;
using Cartex.Application.Common.Interfaces;
using Cartex.Domain.Entities;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Infrastructure.Settings;

public sealed class SettingsService(IApplicationDbContext db) : ISettingsService
{
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var setting = await db.BusinessSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
        return setting?.Value is null ? default : JsonSerializer.Deserialize<T>(setting.Value);
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
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        var setting = await db.BusinessSettings.FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
        if (setting is null) return;
        db.BusinessSettings.Remove(setting);
        await db.SaveChangesAsync(cancellationToken);
    }
}
