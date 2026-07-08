using Cartex.Application.Common.Settings;
using Cartex.Domain.Enums;

namespace Cartex.Infrastructure.Notifications.Sms;

public record SmsSendResult(string? ProviderMessageId);

public interface ISmsProvider
{
    string Name { get; }
    Task<SmsSendResult> SendAsync(SmsSettings settings, string password, string phone, string text, CancellationToken cancellationToken);
    Task<SmsStatus?> GetStatusAsync(SmsSettings settings, string password, string providerMessageId, CancellationToken cancellationToken);
}
