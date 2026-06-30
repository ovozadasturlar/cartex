using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Api.Services;

public sealed class TelegramUpdatePoller(
    IServiceScopeFactory scopeFactory,
    IHttpClientFactory httpFactory,
    ILogger<TelegramUpdatePoller> logger) : BackgroundService
{
    private long _offset;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await PollAsync(stoppingToken); }
            catch (Exception ex) { logger.LogDebug(ex, "Telegram poll error"); }
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }

    private async Task PollAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        var cfg = await settings.GetAsync<TelegramSettings>(SettingKeys.Telegram, ct);
        if (cfg is null || !cfg.Enabled || string.IsNullOrWhiteSpace(cfg.BotToken))
            return;

        var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();
        var token = protector.Unprotect(cfg.BotToken);
        var client = httpFactory.CreateClient();

        var response = await client.GetFromJsonAsync<UpdatesResponse>(
            $"https://api.telegram.org/bot{token}/getUpdates?timeout=0&offset={_offset}", ct);
        if (response is not { Ok: true, Result.Count: > 0 })
            return;

        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        foreach (var update in response.Result)
        {
            _offset = Math.Max(_offset, update.UpdateId + 1);
            var message = update.Message;
            if (message?.Chat is not { } chat)
                continue;

            if (message.Contact?.PhoneNumber is { } phone)
            {
                var digits = new string(phone.Where(char.IsDigit).ToArray());
                var last9 = digits.Length >= 9 ? digits[^9..] : digits;
                var customer = await db.Customers.FirstOrDefaultAsync(c => c.Phone != null && c.Phone.EndsWith(last9), ct);
                if (customer is not null)
                {
                    customer.TelegramChatId = chat.Id.ToString();
                    await db.SaveChangesAsync(ct);
                    await SendAsync(client, token, chat.Id, "Telegram ulandi. Cheklaringiz shu yerga keladi.", ct);
                }
            }
            else if (message.Text == "/start")
            {
                await SendAsync(client, token, chat.Id, "Cheklarni Telegram orqali olish uchun telefon raqamingizni ulashing.", ct);
            }
        }
    }

    private static async Task SendAsync(HttpClient client, string token, long chatId, string text, CancellationToken ct) =>
        await client.PostAsJsonAsync($"https://api.telegram.org/bot{token}/sendMessage", new { chat_id = chatId, text }, ct);

    private sealed record UpdatesResponse(bool Ok, List<Update>? Result);
    private sealed record Update([property: JsonPropertyName("update_id")] long UpdateId, Message? Message);
    private sealed record Message(Chat? Chat, Contact? Contact, string? Text);
    private sealed record Chat(long Id);
    private sealed record Contact([property: JsonPropertyName("phone_number")] string? PhoneNumber);
}
