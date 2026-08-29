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
    public async Task<NotificationProviderResult?> SendAsync(string phone, string text, CancellationToken cancellationToken = default)
        => await SendAsync(phone, text, new SmsSendContext(), cancellationToken);

    public async Task<NotificationProviderResult?> SendAsync(string phone, string text, SmsSendContext context, CancellationToken cancellationToken = default)
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
        var result = await provider.SendAsync(cfg, password, phone, text, context, cancellationToken);
        return new NotificationProviderResult(provider.Name, result.ProviderMessageId, CountSegments(text), result.Pending);
    }

    public static int CountSegments(string text)
    {
        return Cartex.Application.Sms.SmsTextSegments.Count(text);
    }
}
