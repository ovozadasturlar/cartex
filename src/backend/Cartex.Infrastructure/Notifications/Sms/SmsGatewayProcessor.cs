using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Application.Sms;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cartex.Infrastructure.Notifications.Sms;

public sealed class SmsGatewayProcessor(IServiceProvider services, ILogger<SmsGatewayProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SMS gateway processing cycle failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var gateway = scope.ServiceProvider.GetRequiredService<SmsGatewayService>();
        var routing = scope.ServiceProvider.GetRequiredService<SmsGatewayRoutingService>();
        var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();
        var providers = scope.ServiceProvider.GetRequiredService<IEnumerable<ISmsProvider>>();

        await gateway.RequeueExpiredAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var pending = await db.SmsGatewayJobs.Include(x => x.NotificationDelivery).Include(x => x.NotificationDeliveryAttempt)
            .Where(x => x.Status == SmsGatewayJobStatus.Pending && (x.AvailableAt == null || x.AvailableAt <= now))
            .OrderBy(x => x.CreatedAt).Take(100).ToListAsync(cancellationToken);
        foreach (var job in pending)
            await routing.AssignAsync(job, cancellationToken);

        var settings = await settingsService.GetAsync<SmsSettings>(SettingKeys.Sms, cancellationToken) ?? new();
        if (settings.FallbackProvider == "none")
            return;
        var fallback = providers.FirstOrDefault(x => string.Equals(x.Name, settings.FallbackProvider, StringComparison.OrdinalIgnoreCase));
        if (fallback is null || string.Equals(fallback.Name, "device", StringComparison.OrdinalIgnoreCase))
            return;
        var cutoff = DateTime.UtcNow.AddMinutes(-settings.FallbackAfterMinutes);
        var due = pending.Where(x => x.Status == SmsGatewayJobStatus.Pending && x.CreatedAt <= cutoff).ToList();
        var password = string.IsNullOrWhiteSpace(settings.Password) ? string.Empty : protector.Unprotect(settings.Password);
        foreach (var job in due)
        {
            try
            {
                var result = await fallback.SendAsync(settings, password, job.Phone, job.Text,
                    new SmsSendContext(job.BranchId, job.Kind, job.CustomerId, job.IdempotencyKey), cancellationToken);
                job.Status = SmsGatewayJobStatus.Sent;
                job.SentAt = DateTime.UtcNow;
                job.FallbackProvider = fallback.Name;
                job.FallbackMessageId = result.ProviderMessageId;
                if (job.NotificationDeliveryAttempt is not null)
                {
                    job.NotificationDeliveryAttempt.Provider = fallback.Name;
                    job.NotificationDeliveryAttempt.ProviderMessageId = result.ProviderMessageId;
                }
                SmsGatewayDeliverySync.Apply(job);
            }
            catch (Exception ex)
            {
                job.ErrorCode = ex.GetType().Name;
                job.ErrorMessage = ex.Message.Length <= 1000 ? ex.Message : ex.Message[..1000];
            }
        }
        await db.SaveChangesAsync(cancellationToken);
    }
}
