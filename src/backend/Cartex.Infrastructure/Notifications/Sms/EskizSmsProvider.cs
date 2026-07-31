using System.Net.Http.Headers;
using System.Text.Json;
using Cartex.Application.Common.Settings;
using Cartex.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Cartex.Infrastructure.Notifications.Sms;

public sealed class EskizSmsProvider(IHttpClientFactory httpClientFactory, ILogger<EskizSmsProvider> logger) : ISmsProvider
{
    public string Name => "eskiz";

    public async Task<SmsSendResult> SendAsync(SmsSettings settings, string password, string phone, string text, CancellationToken cancellationToken)
    {
        var baseUrl = BaseUrl(settings);
        var client = httpClientFactory.CreateClient();
        var token = await LoginAsync(client, baseUrl, settings, password, cancellationToken);

        using var sendForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["mobile_phone"] = phone,
            ["message"] = text,
            ["from"] = settings.Sender ?? "4546"
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/message/sms/send") { Content = sendForm };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var sendResponse = await client.SendAsync(request, cancellationToken);
        if (!sendResponse.IsSuccessStatusCode)
        {
            logger.LogWarning("Eskiz send failed: {Status}", sendResponse.StatusCode);
            throw new InvalidOperationException($"Eskiz: {sendResponse.StatusCode}");
        }

        using var doc = JsonDocument.Parse(await sendResponse.Content.ReadAsStringAsync(cancellationToken));
        var id = doc.RootElement.TryGetProperty("id", out var idProp)
            ? idProp.ValueKind == JsonValueKind.Number ? idProp.GetInt64().ToString() : idProp.GetString()
            : null;
        return new SmsSendResult(id);
    }

    public async Task<NotificationDeliveryStatus?> GetStatusAsync(SmsSettings settings, string password, string providerMessageId, CancellationToken cancellationToken)
    {
        var baseUrl = BaseUrl(settings);
        var client = httpClientFactory.CreateClient();
        var token = await LoginAsync(client, baseUrl, settings, password, cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/message/sms/status_by_id/{providerMessageId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (!doc.RootElement.TryGetProperty("data", out var data) || !data.TryGetProperty("status", out var statusProp))
            return null;

        return statusProp.GetString()?.ToUpperInvariant() switch
        {
            "DELIVRD" => NotificationDeliveryStatus.Delivered,
            "UNDELIV" or "REJECTD" or "EXPIRED" or "DELETED" => NotificationDeliveryStatus.Undelivered,
            _ => null
        };
    }

    private static string BaseUrl(SmsSettings settings) =>
        string.IsNullOrWhiteSpace(settings.BaseUrl) ? "https://notify.eskiz.uz" : settings.BaseUrl.TrimEnd('/');

    private async Task<string> LoginAsync(HttpClient client, string baseUrl, SmsSettings settings, string password, CancellationToken cancellationToken)
    {
        using var loginForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = settings.Login ?? "",
            ["password"] = password
        });
        var loginResponse = await client.PostAsync($"{baseUrl}/api/auth/login", loginForm, cancellationToken);
        if (!loginResponse.IsSuccessStatusCode)
        {
            logger.LogWarning("Eskiz login failed: {Status}", loginResponse.StatusCode);
            throw new InvalidOperationException($"Eskiz login: {loginResponse.StatusCode}");
        }

        using var doc = JsonDocument.Parse(await loginResponse.Content.ReadAsStringAsync(cancellationToken));
        var token = doc.RootElement.GetProperty("data").GetProperty("token").GetString();
        if (string.IsNullOrEmpty(token))
        {
            logger.LogWarning("Eskiz token missing in response");
            throw new InvalidOperationException("Eskiz: token olinmadi");
        }
        return token;
    }
}
