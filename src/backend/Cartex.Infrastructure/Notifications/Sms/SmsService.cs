using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Microsoft.Extensions.Logging;

namespace Cartex.Infrastructure.Notifications.Sms;

public sealed class SmsService(
    IEnumerable<ISmsProvider> providers,
    ISettingsService settings,
    ISecretProtector protector,
    ILogger<SmsService> logger) : ISmsService
{
    private static readonly HashSet<char> GsmBasic =
        [.. "@£$¥èéùìòÇ\nØø\rÅåΔ_ΦΓΛΩΠΨΣΘΞÆæßÉ !\"#¤%&'()*+,-./0123456789:;<=>?¡ABCDEFGHIJKLMNOPQRSTUVWXYZÄÖÑÜ§¿abcdefghijklmnopqrstuvwxyzäöñüà"];
    private static readonly HashSet<char> GsmExtended = [.. "^{}\\[~]|€"];

    public async Task<NotificationProviderResult?> SendAsync(string phone, string text, CancellationToken cancellationToken = default)
    {
        var cfg = await settings.GetAsync<SmsSettings>(SettingKeys.Sms, cancellationToken);
        if (cfg is null || !cfg.Enabled || string.IsNullOrWhiteSpace(phone))
        {
            logger.LogInformation("SMS not configured; skipped");
            return null;
        }

        var provider = providers.FirstOrDefault(p => string.Equals(p.Name, cfg.Provider, StringComparison.OrdinalIgnoreCase));
        if (provider is null)
        {
            logger.LogWarning("SMS provider '{Provider}' not found", cfg.Provider);
            throw new InvalidOperationException($"SMS provayder topilmadi: {cfg.Provider}");
        }

        var password = string.IsNullOrWhiteSpace(cfg.Password) ? "" : protector.Unprotect(cfg.Password);
        var result = await provider.SendAsync(cfg, password, phone, text, cancellationToken);
        return new NotificationProviderResult(provider.Name, result.ProviderMessageId, CountSegments(text));
    }

    public static int CountSegments(string text)
    {
        var septets = 0;
        foreach (var character in text)
        {
            if (GsmBasic.Contains(character))
                septets++;
            else if (GsmExtended.Contains(character))
                septets += 2;
            else
                return text.Length <= 70 ? 1 : (int)Math.Ceiling((double)text.Length / 67);
        }

        return septets <= 160 ? 1 : (int)Math.Ceiling((double)septets / 153);
    }
}
