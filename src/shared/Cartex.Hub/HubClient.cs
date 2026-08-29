using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Cartex.Shared.Models.OfflineCache;

namespace Cartex.Hub;

/// Yo'ldosh tomoni. HUB-12: bu yerdan HUB'ga faqat guvohnoma ketadi — bulut tokeni hech qachon.
/// `hubPublicKey` — topish bosqichida tasdiqlangan HUB kaliti: har so'rov qo'l siqishida aynan shu
/// kalitga bog'lanadi, ya'ni guvohnomani ushlab olgan soxta HUB javob bera olmaydi.
public sealed class HubClient(HttpClient http, string attestationToken, string hubPublicKey)
{
    public Task<OfflineSnapshotDto?> CatalogAsync(Uri endpoint, DateTime? since, CancellationToken cancellationToken) =>
        SendAsync<OfflineSnapshotDto>(
            new HttpRequestMessage(HttpMethod.Get, new Uri(endpoint,
                "hub/catalog" + (since is null ? "" : "?since=" + Uri.EscapeDataString(since.Value.ToString("o"))))),
            cancellationToken);

    public Task<HubEventResult?> SendAsync(
        Uri endpoint, OfflineSyncEventRequest request, CancellationToken cancellationToken) =>
        SendAsync<HubEventResult>(
            new HttpRequestMessage(HttpMethod.Post, new Uri(endpoint, "hub/events"))
            {
                // Tana oldindan matnga o'giriladi: `JsonContent` uzunlikni bilmay chunked yuboradi,
                // HUB tinglovchisi esa faqat `Content-Length` bilan ishlaydi.
                Content = new StringContent(
                    JsonSerializer.Serialize(request, HubJson.Options), Encoding.UTF8, "application/json")
            },
            cancellationToken);

    private async Task<T?> SendAsync<T>(HttpRequestMessage message, CancellationToken cancellationToken)
    {
        using (message)
        {
            message.Headers.Add(HubHeaders.Attestation, attestationToken);
            message.Options.Set(HubTransport.ExpectedKey, hubPublicKey);
            using var response = await http.SendAsync(message, cancellationToken);
            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<T>(HubJson.Options, cancellationToken)
                : default;
        }
    }
}
