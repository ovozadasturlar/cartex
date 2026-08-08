using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Store.Commands;

public record TelegramStoreLoginCommand(string InitData, string? DeviceName = null) : IRequest<StoreLoginResponse>;

public sealed class TelegramStoreLoginCommandHandler(
    IApplicationDbContext db,
    IFeatureStateProvider features,
    ISettingsService settings,
    ISecretProtector protector,
    StoreTokenBuilder tokenBuilder) : IRequestHandler<TelegramStoreLoginCommand, StoreLoginResponse>
{
    private const string InvalidMessage = "Telegram ma'lumotlari noto'g'ri yoki eskirgan.";

    public async Task<StoreLoginResponse> Handle(TelegramStoreLoginCommand request, CancellationToken cancellationToken)
    {
        if (!await features.IsEnabledAsync(FeatureCatalog.Ordering, cancellationToken))
            throw new ForbiddenException("Onlayn buyurtma o'chirilgan.");

        var cfg = await settings.GetAsync<TelegramSettings>(SettingKeys.Telegram, cancellationToken);
        if (cfg is not { Enabled: true } || string.IsNullOrWhiteSpace(cfg.BotToken))
            throw new UnauthorizedAccessException("Telegram orqali kirish yoqilmagan.");

        var fields = ValidateInitData(request.InitData, protector.Unprotect(cfg.BotToken));
        var telegramId = ExtractUserId(fields);

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.TelegramChatId == telegramId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Telegram hisobi bog'lanmagan.");

        return await tokenBuilder.IssueAsync(customer, request.DeviceName, cancellationToken);
    }

    private static Dictionary<string, string> ValidateInitData(string initData, string botToken)
    {
        var fields = new Dictionary<string, string>();
        foreach (var pair in initData.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = pair.IndexOf('=');
            var key = WebUtility.UrlDecode(idx < 0 ? pair : pair[..idx]);
            fields[key] = idx < 0 ? "" : WebUtility.UrlDecode(pair[(idx + 1)..]);
        }

        if (!fields.Remove("hash", out var hash))
            throw new UnauthorizedAccessException(InvalidMessage);

        var dataCheckString = string.Join('\n', fields
            .OrderBy(f => f.Key, StringComparer.Ordinal)
            .Select(f => $"{f.Key}={f.Value}"));

        var secretKey = HMACSHA256.HashData(Encoding.UTF8.GetBytes("WebAppData"), Encoding.UTF8.GetBytes(botToken));
        var computed = HMACSHA256.HashData(secretKey, Encoding.UTF8.GetBytes(dataCheckString));

        byte[] provided;
        try { provided = Convert.FromHexString(hash); }
        catch (FormatException) { throw new UnauthorizedAccessException(InvalidMessage); }

        if (!CryptographicOperations.FixedTimeEquals(computed, provided))
            throw new UnauthorizedAccessException(InvalidMessage);

        if (!fields.TryGetValue("auth_date", out var authDateRaw)
            || !long.TryParse(authDateRaw, out var authDate)
            || DateTimeOffset.UtcNow.ToUnixTimeSeconds() - authDate > 86400)
            throw new UnauthorizedAccessException(InvalidMessage);

        return fields;
    }

    private static string ExtractUserId(Dictionary<string, string> fields)
    {
        if (!fields.TryGetValue("user", out var userJson))
            throw new UnauthorizedAccessException(InvalidMessage);
        try
        {
            using var doc = JsonDocument.Parse(userJson);
            return doc.RootElement.GetProperty("id").GetInt64().ToString();
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new UnauthorizedAccessException(InvalidMessage);
        }
    }
}

public sealed class TelegramStoreLoginCommandValidator : AbstractValidator<TelegramStoreLoginCommand>
{
    public TelegramStoreLoginCommandValidator()
    {
        RuleFor(x => x.InitData).NotEmpty().MaximumLength(4096);
    }
}
