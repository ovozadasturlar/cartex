using Cartex.Domain.Enums;

namespace Cartex.Application.Common.Interfaces;

public sealed record NotificationMessage(
    NotificationChannel Channel,
    string Recipient,
    string Template,
    IReadOnlyDictionary<string, string> Data,
    long? CustomerId = null);

public sealed record NotificationProviderResult(
    string Provider,
    string? ProviderMessageId = null,
    int Units = 1);

public interface INotificationService
{
    Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default);
}
