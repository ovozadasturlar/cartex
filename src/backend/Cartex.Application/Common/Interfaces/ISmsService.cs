namespace Cartex.Application.Common.Interfaces;

public interface ISmsService
{
    Task<NotificationProviderResult?> SendAsync(string phone, string text, CancellationToken cancellationToken = default);
}
