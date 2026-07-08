namespace Cartex.Application.Common.Interfaces;

public record PushMessage(string DeviceToken, string Title, string Body, IReadOnlyDictionary<string, string>? Data = null);

public interface IPushGateway
{
    bool IsEnabled { get; }

    Task SendAsync(PushMessage message, CancellationToken cancellationToken = default);
}
