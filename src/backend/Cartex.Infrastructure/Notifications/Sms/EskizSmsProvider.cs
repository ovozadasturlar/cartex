using System.Net.Http.Headers;
using System.Text.Json;
using Cartex.Application.Common.Settings;
using Microsoft.Extensions.Logging;

namespace Cartex.Infrastructure.Notifications.Sms;

public sealed class EskizSmsProvider(IHttpClientFactory httpClientFactory, ILogger<EskizSmsProvider> logger) : ISmsProvider
{
    public string Name => "eskiz";

    public async Task SendAsync(SmsSettings settings, string password, string phone, string text, CancellationToken cancellationToken)
    {
        var baseUrl = string.IsNullOrWhiteSpace(settings.BaseUrl) ? "https://notify.eskiz.uz" : settings.BaseUrl.TrimEnd('/');
        var client = httpClientFactory.CreateClient();

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
    }
}
