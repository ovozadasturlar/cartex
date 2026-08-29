namespace Cartex.Application.Common.Interfaces;

public record TelegramBotInfo(bool Ok, string? Username);

public interface ITelegramService
{
    Task<NotificationProviderResult?> SendMessageAsync(string chatId, string text, CancellationToken cancellationToken = default);
    Task<NotificationProviderResult?> SendDocumentAsync(string chatId, byte[] content, string fileName, string? caption, CancellationToken cancellationToken = default);
    Task<TelegramBotInfo> ValidateAsync(string token, CancellationToken cancellationToken = default);
}
