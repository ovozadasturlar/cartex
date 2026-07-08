using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.Extensions.Logging;

namespace Cartex.Infrastructure.Notifications.Sms;

public sealed class SmsService(
    IEnumerable<ISmsProvider> providers,
    ISettingsService settings,
    ISecretProtector protector,
    IApplicationDbContext db,
    ILogger<SmsService> logger) : ISmsService
{
    public async Task SendAsync(string phone, string text, CancellationToken cancellationToken = default)
    {
        var cfg = await settings.GetAsync<SmsSettings>(SettingKeys.Sms, cancellationToken);
        if (cfg is null || !cfg.Enabled || string.IsNullOrWhiteSpace(phone))
        {
            logger.LogInformation("SMS not configured; skipped");
            return;
        }

        var provider = providers.FirstOrDefault(p => string.Equals(p.Name, cfg.Provider, StringComparison.OrdinalIgnoreCase));
        if (provider is null)
        {
            logger.LogWarning("SMS provider '{Provider}' not found", cfg.Provider);
            throw new InvalidOperationException($"SMS provayder topilmadi: {cfg.Provider}");
        }

        var password = string.IsNullOrWhiteSpace(cfg.Password) ? "" : protector.Unprotect(cfg.Password);
        var message = new SmsMessage
        {
            Phone = phone,
            Text = text,
            Provider = provider.Name,
            Segments = CountSegments(text)
        };

        try
        {
            var result = await provider.SendAsync(cfg, password, phone, text, cancellationToken);
            message.Status = SmsStatus.Sent;
            message.ProviderMessageId = result.ProviderMessageId;
        }
        catch (Exception ex)
        {
            message.Status = SmsStatus.Failed;
            message.Error = ex.Message;
            db.SmsMessages.Add(message);
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }

        db.SmsMessages.Add(message);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static int CountSegments(string text)
    {
        var ascii = text.All(c => c <= 127);
        var single = ascii ? 160 : 70;
        var multi = ascii ? 153 : 67;
        return text.Length <= single ? 1 : (int)Math.Ceiling((double)text.Length / multi);
    }
}
