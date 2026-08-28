using Cartex.Application.Common.Interfaces;

namespace Cartex.Application.Tests.Common;

public sealed class RecordingMessageChannels : ITelegramService, ISmsService, IEmailService
{
    public List<string> Used { get; } = [];

    public string? LastText { get; private set; }

    public void Clear()
    {
        Used.Clear();
        LastText = null;
    }

    public Task<NotificationProviderResult?> SendMessageAsync(string chatId, string text, CancellationToken cancellationToken = default) =>
        Record("telegram", text);

    public Task<NotificationProviderResult?> SendDocumentAsync(string chatId, byte[] content, string fileName, string? caption, CancellationToken cancellationToken = default) =>
        Record("telegram-document", caption ?? fileName);

    public Task<TelegramBotInfo> ValidateAsync(string token, CancellationToken cancellationToken = default) =>
        Task.FromResult(new TelegramBotInfo(true, "cartex_bot"));

    public Task<NotificationProviderResult?> SendAsync(string phone, string text, CancellationToken cancellationToken = default) =>
        Record("sms", text);

    public Task<NotificationProviderResult?> SendAsync(string phone, string text, SmsSendContext context, CancellationToken cancellationToken = default) =>
        Record("sms", text);

    public Task<NotificationProviderResult?> SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default, EmailAttachment? attachment = null) =>
        Record("email", body);

    private Task<NotificationProviderResult?> Record(string channel, string text)
    {
        Used.Add(channel);
        LastText = text;
        return Task.FromResult<NotificationProviderResult?>(new NotificationProviderResult(channel));
    }
}
