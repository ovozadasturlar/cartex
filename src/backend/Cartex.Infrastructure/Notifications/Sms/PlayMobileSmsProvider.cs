using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Cartex.Application.Common.Settings;
using Cartex.Application.Common.Interfaces;
using Cartex.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Cartex.Infrastructure.Notifications.Sms;

public sealed class PlayMobileSmsProvider(IHttpClientFactory httpClientFactory, ILogger<PlayMobileSmsProvider> logger) : ISmsProvider
{
    public string Name => "playmobile";

    public async Task<SmsSendResult> SendAsync(SmsSettings settings, string password, string phone, string text, SmsSendContext context, CancellationToken cancellationToken)
    {
        var baseUrl = string.IsNullOrWhiteSpace(settings.BaseUrl) ? "https://send.smsxabar.uz" : settings.BaseUrl.TrimEnd('/');
        var client = httpClientFactory.CreateClient();

        var messageId = Guid.NewGuid().ToString("N");
        var message = new Dictionary<string, object>
        {
            ["recipient"] = phone.Trim().TrimStart('+'),
            ["message-id"] = messageId,
            ["sms"] = new { originator = settings.Sender ?? "", content = new { text } }
        };
        var payload = new Dictionary<string, object> { ["messages"] = new[] { message } };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/broker-api/send");
        request.Content = JsonContent.Create(payload);
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.Login}:{password}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

        var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("PlayMobile send failed: {Status}", response.StatusCode);
            throw new InvalidOperationException($"PlayMobile: {response.StatusCode}");
        }

        return new SmsSendResult(messageId);
    }

    public Task<NotificationDeliveryStatus?> GetStatusAsync(SmsSettings settings, string password, string providerMessageId, CancellationToken cancellationToken) =>
        Task.FromResult<NotificationDeliveryStatus?>(null);
}
