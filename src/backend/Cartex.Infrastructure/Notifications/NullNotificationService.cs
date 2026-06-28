using Cartex.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Cartex.Infrastructure.Notifications;

public sealed class NullNotificationService(ILogger<NullNotificationService> logger) : INotificationService
{
    public Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Notification skipped (no adapter): {Channel} -> {Recipient} [{Template}]",
            message.Channel, message.Recipient, message.Template);
        return Task.CompletedTask;
    }
}
