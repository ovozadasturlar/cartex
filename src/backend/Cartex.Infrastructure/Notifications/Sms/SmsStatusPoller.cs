using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cartex.Infrastructure.Notifications.Sms;

public sealed class SmsStatusPoller(
    IServiceScopeFactory scopeFactory,
    ILogger<SmsStatusPoller> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await PollAsync(stoppingToken); }
            catch (Exception ex) { logger.LogWarning(ex, "SMS status poll error"); }
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private async Task PollAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        var cfg = await settings.GetAsync<SmsSettings>(SettingKeys.Sms, ct);
        if (cfg is null || !cfg.Enabled)
            return;

        var providers = scope.ServiceProvider.GetRequiredService<IEnumerable<ISmsProvider>>();
        var provider = providers.FirstOrDefault(p => string.Equals(p.Name, cfg.Provider, StringComparison.OrdinalIgnoreCase));
        if (provider is null)
            return;

        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var cutoff = DateTime.UtcNow.AddHours(-48);
        var pending = await db.SmsMessages
            .Where(m => m.Status == SmsStatus.Sent && m.ProviderMessageId != null
                && m.Provider == provider.Name && m.CreatedAt > cutoff)
            .OrderBy(m => m.Id)
            .Take(50)
            .ToListAsync(ct);
        if (pending.Count == 0)
            return;

        var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();
        var password = string.IsNullOrWhiteSpace(cfg.Password) ? "" : protector.Unprotect(cfg.Password);

        foreach (var message in pending)
        {
            var status = await provider.GetStatusAsync(cfg, password, message.ProviderMessageId!, ct);
            if (status is null)
                continue;
            message.Status = status.Value;
            if (status == SmsStatus.Delivered)
                message.DeliveredAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }
}
