using Cartex.Application.Common.Settings;
using Cartex.Application.Common.Interfaces;
using Cartex.Domain.Enums;

namespace Cartex.Infrastructure.Notifications.Sms;

public record SmsSendResult(string? ProviderMessageId, bool Pending = false);

public interface ISmsProvider
{
    string Name { get; }
    Task<SmsSendResult> SendAsync(SmsSettings settings, string password, string phone, string text, SmsSendContext context, CancellationToken cancellationToken);
    Task<NotificationDeliveryStatus?> GetStatusAsync(SmsSettings settings, string password, string providerMessageId, CancellationToken cancellationToken);
}
