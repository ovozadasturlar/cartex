using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Cartex.Application.Common;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Api.Services;

public sealed class TelegramUpdatePoller(
    IServiceScopeFactory scopeFactory,
    IHttpClientFactory httpFactory,
    ILogger<TelegramUpdatePoller> logger) : BackgroundService
{
    private long _offset;
    private bool _conflictWarned;
    private readonly Dictionary<long, string> _pendingLanguage = [];

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

        UpdatesResponse? response;
        try
        {
            response = await client.GetFromJsonAsync<UpdatesResponse>(
                $"https://api.telegram.org/bot{token}/getUpdates?timeout=0&offset={_offset}", ct);
            _conflictWarned = false;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            if (!_conflictWarned)
            {
                _conflictWarned = true;
                logger.LogWarning("Telegram getUpdates 409 Conflict: bu bot token boshqa joyda ham ishlatilmoqda (boshqa server nusxasi yoki o'rnatilgan webhook). Bot xabarlarni qabul qila olmaydi — webhookni o'chiring (deleteWebhook) yoki alohida bot token ishlating.");
            }
            return;
        }
        catch (HttpRequestException ex) when (ex.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.NotFound)
        {
            cfg.Enabled = false;
            await settings.SetAsync(SettingKeys.Telegram, cfg, ct);
            logger.LogWarning("Telegram bot tokeni yaroqsiz — integratsiya avtomatik o'chirildi. Sozlamalar → Integratsiyalar'da yangi token bog'lang.");
            return;
        }

        if (response is not { Ok: true, Result.Count: > 0 })
            return;

        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var publicBaseUrl = (await settings.GetAsync<NotificationSettings>(SettingKeys.Notification, ct))?.PublicBaseUrl?.TrimEnd('/');

        foreach (var update in response.Result)
        {
            _offset = Math.Max(_offset, update.UpdateId + 1);
            if (update.CallbackQuery is { } callback)
                await HandleCallbackAsync(db, client, token, callback, ct);
            else if (update.Message is { Chat: { } } message)
                await HandleMessageAsync(db, client, token, message, publicBaseUrl, ct);
        }
    }

    private async Task HandleCallbackAsync(IApplicationDbContext db, HttpClient client, string token, CallbackQuery callback, CancellationToken ct)
    {
        await client.PostAsJsonAsync($"https://api.telegram.org/bot{token}/answerCallbackQuery",
            new Dictionary<string, object?> { ["callback_query_id"] = callback.Id }, ct);

        if (callback.Data is not { } data || !data.StartsWith("lang:") || callback.Message?.Chat is not { } chat)
            return;

        var lang = data[5..];
        if (!TelegramBotTexts.Languages.Contains(lang))
            return;

        var customer = await FindByChatAsync(db, chat.Id, ct);
        if (customer is not null)
        {
            customer.PreferredLanguage = lang;
            await db.SaveChangesAsync(ct);
            await SendMenuAsync(client, token, chat.Id, lang, ct);
        }
        else
        {
            _pendingLanguage[chat.Id] = lang;
            await SendSharePhoneAsync(client, token, chat.Id, lang, ct);
        }
    }

    private async Task HandleMessageAsync(IApplicationDbContext db, HttpClient client, string token, Message message, string? publicBaseUrl, CancellationToken ct)
    {
        var chatId = message.Chat!.Id;

        if (message.Contact?.PhoneNumber is { } rawPhone)
        {
            var phone = Phones.Normalize(rawPhone);
            var customer = phone is null ? null : await db.Customers.FirstOrDefaultAsync(c => c.Phone == phone, ct);
            _pendingLanguage.Remove(chatId, out var pending);
            var lang = pending ?? customer?.PreferredLanguage ?? TelegramBotTexts.DefaultLanguage;

            if (customer is null)
            {
                await SendAsync(client, token, chatId, TelegramBotTexts.Get("not_found", lang), null, ct);
                return;
            }

            customer.TelegramChatId = chatId.ToString();
            if (pending is not null) customer.PreferredLanguage = pending;
            await db.SaveChangesAsync(ct);
            await SendAsync(client, token, chatId, TelegramBotTexts.Get("linked", lang), MenuKeyboard(lang), ct);
            return;
        }

        var bound = await FindByChatAsync(db, chatId, ct);
        var language = bound?.PreferredLanguage ?? _pendingLanguage.GetValueOrDefault(chatId) ?? TelegramBotTexts.DefaultLanguage;

        if (message.Text == "/start")
        {
            await SendLanguagePickerAsync(client, token, chatId, language, ct);
            return;
        }

        if (bound is null)
        {
            await SendSharePhoneAsync(client, token, chatId, language, ct);
            return;
        }

        switch (TelegramBotTexts.ActionFor(message.Text))
        {
            case "sales":
                await SendSalesAsync(db, client, token, chatId, bound, language, publicBaseUrl, ct);
                break;
            case "balance":
                await SendBalanceAsync(db, client, token, chatId, bound, language, ct);
                break;
            case "lang":
                await SendLanguagePickerAsync(client, token, chatId, language, ct);
                break;
            default:
                await SendMenuAsync(client, token, chatId, language, ct);
                break;
        }
    }

    private async Task SendSalesAsync(IApplicationDbContext db, HttpClient client, string token, long chatId, Customer customer, string lang, string? publicBaseUrl, CancellationToken ct)
    {
        var baseCode = await db.Businesses.Select(b => b.Currency).FirstAsync(ct);
        var sales = await db.Sales
            .Where(s => s.CustomerId == customer.Id)
            .OrderByDescending(s => s.Id)
            .Take(5)
            .Select(s => new { s.CreatedAt, s.TotalAmount, s.ReceiptToken })
            .ToListAsync(ct);

        if (sales.Count == 0)
        {
            await SendAsync(client, token, chatId, TelegramBotTexts.Get("no_sales", lang), MenuKeyboard(lang), ct);
            return;
        }

        var lines = sales.Select(s =>
        {
            var line = $"📅 {s.CreatedAt:dd.MM.yyyy HH:mm} — {s.TotalAmount:N0} {baseCode}";
            return publicBaseUrl is null ? line : $"{line}\n{publicBaseUrl}/r/{s.ReceiptToken}";
        });
        var text = $"{TelegramBotTexts.Get("sales_header", lang)}\n\n{string.Join("\n\n", lines)}";
        await SendAsync(client, token, chatId, text, MenuKeyboard(lang), ct);
    }

    private async Task SendBalanceAsync(IApplicationDbContext db, HttpClient client, string token, long chatId, Customer customer, string lang, CancellationToken ct)
    {
        var baseCode = await db.Businesses.Select(b => b.Currency).FirstAsync(ct);
        var debt = await db.Accounts
            .Where(a => a.CustomerId == customer.Id && a.Type == AccountType.Debt)
            .SumAsync(a => a.Balance * (a.Currency == baseCode ? 1m
                : db.ExchangeRates.Where(r => r.Code == a.Currency).OrderByDescending(r => r.EffectiveAt).Select(r => r.Rate).FirstOrDefault()), ct);
        var bonus = await db.Accounts
            .Where(a => a.CustomerId == customer.Id && a.Type == AccountType.Bonus)
            .SumAsync(a => a.Balance, ct);

        var text = $"{TelegramBotTexts.Get("debt", lang)}: {debt:N0} {baseCode}\n{TelegramBotTexts.Get("bonus", lang)}: {bonus:N0} {baseCode}";
        await SendAsync(client, token, chatId, text, MenuKeyboard(lang), ct);
    }

    private Task SendMenuAsync(HttpClient client, string token, long chatId, string lang, CancellationToken ct) =>
        SendAsync(client, token, chatId, TelegramBotTexts.Get("menu", lang), MenuKeyboard(lang), ct);

    private Task SendSharePhoneAsync(HttpClient client, string token, long chatId, string lang, CancellationToken ct) =>
        SendAsync(client, token, chatId, TelegramBotTexts.Get("share_phone", lang), new
        {
            keyboard = new[] { new[] { new { text = TelegramBotTexts.Get("btn_share", lang), request_contact = true } } },
            resize_keyboard = true,
            one_time_keyboard = true
        }, ct);

    private Task SendLanguagePickerAsync(HttpClient client, string token, long chatId, string lang, CancellationToken ct) =>
        SendAsync(client, token, chatId, TelegramBotTexts.Get("choose_language", lang), new
        {
            inline_keyboard = TelegramBotTexts.Languages
                .Chunk(2)
                .Select(row => row.Select(code => new { text = TelegramBotTexts.LanguageNames[code], callback_data = $"lang:{code}" }).ToArray())
                .ToArray()
        }, ct);

    private static Task<Customer?> FindByChatAsync(IApplicationDbContext db, long chatId, CancellationToken ct)
    {
        var id = chatId.ToString();
        return db.Customers.FirstOrDefaultAsync(c => c.TelegramChatId == id, ct);
    }

    private static object MenuKeyboard(string lang) => new
    {
        keyboard = new[]
        {
            new[] { new { text = TelegramBotTexts.Get("btn_sales", lang) }, new { text = TelegramBotTexts.Get("btn_balance", lang) } },
            new[] { new { text = TelegramBotTexts.Get("btn_lang", lang) } }
        },
        resize_keyboard = true
    };

    private static async Task SendAsync(HttpClient client, string token, long chatId, string text, object? replyMarkup, CancellationToken ct)
    {
        var body = new Dictionary<string, object?> { ["chat_id"] = chatId, ["text"] = text };
        if (replyMarkup is not null) body["reply_markup"] = replyMarkup;
        await client.PostAsJsonAsync($"https://api.telegram.org/bot{token}/sendMessage", body, ct);
    }

    private sealed record UpdatesResponse(bool Ok, List<Update>? Result);
    private sealed record Update(
        [property: JsonPropertyName("update_id")] long UpdateId,
        Message? Message,
        [property: JsonPropertyName("callback_query")] CallbackQuery? CallbackQuery);
    private sealed record Message(Chat? Chat, Contact? Contact, string? Text);
    private sealed record Chat(long Id);
    private sealed record Contact([property: JsonPropertyName("phone_number")] string? PhoneNumber);
    private sealed record CallbackQuery(string Id, string? Data, Message? Message);
}
