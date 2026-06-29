using Cartex.Application.Common.Settings;

namespace Cartex.Infrastructure.Notifications.Sms;

public interface ISmsProvider
{
    string Name { get; }
    Task SendAsync(SmsSettings settings, string password, string phone, string text, CancellationToken cancellationToken);
}
