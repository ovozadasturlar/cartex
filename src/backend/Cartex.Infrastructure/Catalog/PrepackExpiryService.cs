using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cartex.Infrastructure.Catalog;

public sealed class PrepackExpiryService(
    IServiceScopeFactory scopeFactory,
    ILogger<PrepackExpiryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                var now = DateTime.UtcNow;
                await db.Prepacks
                    .Where(p => p.Status == PrepackStatus.Active && p.ExpiresAt != null && p.ExpiresAt <= now)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, PrepackStatus.Expired), stoppingToken);
            }
            catch (Exception ex) { logger.LogWarning(ex, "Prepack expiry error"); }
            await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
        }
    }
}
