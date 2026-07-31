using System.Net.Http.Json;
using System.Text.Json;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Microsoft.Extensions.Logging;

namespace Cartex.Infrastructure.Notifications.Telegram;

public sealed class TelegramService(
    IHttpClientFactory httpClientFactory,
    ISettingsService settings,
    ISecretProtector protector,
    ILogger<TelegramService> logger) : ITelegramService
{
    public async Task<NotificationProviderResult?> SendMessageAsync(string chatId, string text, CancellationToken cancellationToken = default)
    {
        var cfg = await settings.GetAsync<TelegramSettings>(SettingKeys.Telegram, cancellationToken);
        if (cfg is null || !cfg.Enabled || string.IsNullOrWhiteSpace(cfg.BotToken) || string.IsNullOrWhiteSpace(chatId))
        {
            logger.LogInformation("Telegram not configured; skipped");
            return null;
        }

        var token = protector.Unprotect(cfg.BotToken);
        var client = httpClientFactory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"https://api.telegram.org/bot{token}/sendMessage",
            new { chat_id = chatId, text },
            cancellationToken);

        return new NotificationProviderResult("api.telegram.org", await EnsureOkAsync(response, cancellationToken));
    }

    public async Task<NotificationProviderResult?> SendDocumentAsync(string chatId, byte[] content, string fileName, string? caption, CancellationToken cancellationToken = default)
    {
        var cfg = await settings.GetAsync<TelegramSettings>(SettingKeys.Telegram, cancellationToken);
        if (cfg is null || !cfg.Enabled || string.IsNullOrWhiteSpace(cfg.BotToken) || string.IsNullOrWhiteSpace(chatId))
        {
            logger.LogInformation("Telegram not configured; skipped");
            return null;
        }

        var token = protector.Unprotect(cfg.BotToken);
        var client = httpClientFactory.CreateClient();
        using var form = new MultipartFormDataContent
        {
            { new StringContent(chatId), "chat_id" },
            { new ByteArrayContent(content), "document", fileName }
        };
        if (!string.IsNullOrWhiteSpace(caption))
            form.Add(new StringContent(caption), "caption");

        var response = await client.PostAsync($"https://api.telegram.org/bot{token}/sendDocument", form, cancellationToken);
        return new NotificationProviderResult("api.telegram.org", await EnsureOkAsync(response, cancellationToken));
    }

    private async Task<string?> EnsureOkAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.IsSuccessStatusCode)
            return TryMessageId(body);

        var description = TryDescription(body) ?? response.StatusCode.ToString();
        logger.LogWarning("Telegram send failed: {Description}", description);
        throw new InvalidOperationException($"Telegram: {description}");
    }

    private static string? TryMessageId(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("result", out var result)
                && result.TryGetProperty("message_id", out var id)
                ? id.GetInt64().ToString()
                : null;
        }
        catch { return null; }
    }

    private static string? TryDescription(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("description", out var d) ? d.GetString() : null;
        }
        catch { return null; }
    }

    public async Task<TelegramBotInfo> ValidateAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return new TelegramBotInfo(false, null);
        try
        {
            var client = httpClientFactory.CreateClient();
            var response = await client.GetFromJsonAsync<GetMeResponse>(
                $"https://api.telegram.org/bot{token}/getMe", cancellationToken);
            return response is { Ok: true, Result: not null }
                ? new TelegramBotInfo(true, response.Result.Username)
                : new TelegramBotInfo(false, null);
        }
        catch
        {
            return new TelegramBotInfo(false, null);
        }
    }

    private sealed record GetMeResponse(bool Ok, GetMeResult? Result);
    private sealed record GetMeResult(string? Username);
}
