namespace Cartex.Application.Common.Interfaces;

public enum NotificationChannel
{
    AppPush,
    Telegram,
    Sms,
    Email
}

public sealed record NotificationMessage(
    NotificationChannel Channel,
    string Recipient,
    string Template,
    IReadOnlyDictionary<string, string> Data);

public interface INotificationService
{
    Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default);
}
