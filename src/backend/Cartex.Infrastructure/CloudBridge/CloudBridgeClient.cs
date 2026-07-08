using System.Net.Http.Json;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;

namespace Cartex.Infrastructure.CloudBridge;

public sealed class CloudBridgeClient(ISettingsService settings, ISecretProtector protector, IHttpClientFactory httpFactory)
{
    public async Task<CloudBridgeSettings?> GetActiveAsync(CancellationToken cancellationToken)
    {
        var cfg = await settings.GetAsync<CloudBridgeSettings>(SettingKeys.CloudBridge, cancellationToken);
        return cfg is { Enabled: true } && !string.IsNullOrWhiteSpace(cfg.GatewayUrl) ? cfg : null;
    }

    public async Task PushReceiptAsync(CloudBridgeSettings cfg, string token, string html, byte[] pdf, CancellationToken cancellationToken)
    {
        var client = httpFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{cfg.GatewayUrl!.TrimEnd('/')}/api/receipts")
        {
            Content = JsonContent.Create(new { token, html, pdfBase64 = Convert.ToBase64String(pdf) })
        };
        if (!string.IsNullOrEmpty(cfg.LicenseKey))
            request.Headers.Add("X-License-Key", protector.Unprotect(cfg.LicenseKey));
        var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
