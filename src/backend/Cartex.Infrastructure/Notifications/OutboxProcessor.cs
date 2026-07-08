using System.Reflection;
using System.Text.Json;
using Cartex.Application.Common.Messaging;
using Cartex.Domain.Common;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cartex.Infrastructure.Notifications;

public sealed class OutboxProcessor(IServiceProvider services, ILogger<OutboxProcessor> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);
    private const int BatchSize = 20;
    private const int MaxAttempts = 12;
    private DateTime _lastCleanup = DateTime.MinValue;

    private static readonly Assembly DomainAssembly = typeof(IDomainEvent).Assembly;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox processing cycle failed");
            }

            try { await Task.Delay(PollInterval, stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }

    private async Task ProcessPendingAsync(CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        if (DateTime.UtcNow - _lastCleanup > TimeSpan.FromHours(24))
        {
            _lastCleanup = DateTime.UtcNow;
            var cutoff = DateTime.UtcNow.AddDays(-30);
            await db.NotificationOutbox
                .Where(m => m.Status == OutboxStatus.Processed && m.ProcessedAt < cutoff)
                .ExecuteDeleteAsync(cancellationToken);
        }

        var now = DateTime.UtcNow;
        var pending = await db.NotificationOutbox
            .Where(m => m.Status == OutboxStatus.Pending && (m.NextAttemptAt == null || m.NextAttemptAt <= now))
            .OrderBy(m => m.OccurredAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
            return;

        foreach (var message in pending)
        {
            try
            {
                var notification = Deserialize(message.EventType, message.Payload);
                if (notification is null)
                {
                    message.Status = OutboxStatus.Failed;
                    message.Error = $"Unknown event type: {message.EventType}";
                    continue;
                }

                await publisher.Publish(notification, cancellationToken);
                message.Status = OutboxStatus.Processed;
                message.ProcessedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                message.Attempts++;
                message.Error = ex.Message;
                if (message.Attempts >= MaxAttempts)
                    message.Status = OutboxStatus.Failed;
                else
                    message.NextAttemptAt = DateTime.UtcNow.AddMinutes(Math.Min(Math.Pow(2, message.Attempts), 60));
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static INotification? Deserialize(string eventTypeName, string payload)
    {
        var eventType = DomainAssembly.GetType(eventTypeName);
        if (eventType is null)
            return null;

        if (JsonSerializer.Deserialize(payload, eventType) is not IDomainEvent domainEvent)
            return null;

        var notificationType = typeof(DomainEventNotification<>).MakeGenericType(eventType);
        return (INotification)Activator.CreateInstance(notificationType, domainEvent)!;
    }
}
