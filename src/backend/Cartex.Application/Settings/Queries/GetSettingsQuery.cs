using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Microsoft.Extensions.Logging;
using Cartex.Shared.Models.Settings;

namespace Cartex.Application.Settings.Queries;

public record GetSettingsQuery : IRequest<SettingsDto>;

public sealed class GetSettingsQueryHandler(
    ISettingsService settings,
    ISecretProtector protector,
    ILogger<GetSettingsQueryHandler> logger) : IRequestHandler<GetSettingsQuery, SettingsDto>
{
    public async Task<SettingsDto> Handle(GetSettingsQuery request, CancellationToken cancellationToken)
    {
        var tg = await settings.GetAsync<TelegramSettings>(SettingKeys.Telegram, cancellationToken) ?? new();
        var em = await settings.GetAsync<EmailSettings>(SettingKeys.Email, cancellationToken) ?? new();
        var sms = await settings.GetAsync<SmsSettings>(SettingKeys.Sms, cancellationToken) ?? new();
        var notif = await settings.GetAsync<NotificationSettings>(SettingKeys.Notification, cancellationToken) ?? new();

        var tokenLength = 0;
        if (!string.IsNullOrEmpty(tg.BotToken))
        {
            try
            {
                tokenLength = protector.Unprotect(tg.BotToken).Length;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not read the stored Telegram bot token length");
            }
        }

        return new SettingsDto(
            new TelegramSettingsDto(tg.Enabled, tg.ChatId, !string.IsNullOrEmpty(tg.BotToken), tokenLength),
            new EmailSettingsDto(em.Enabled, em.Host, em.Port, em.UseSsl, em.Username, em.FromAddress, em.FromName, !string.IsNullOrEmpty(em.Password)),
            new SmsSettingsDto(sms.Enabled, sms.Provider, sms.Login, sms.Sender, sms.BaseUrl, !string.IsNullOrEmpty(sms.Password),
                sms.FallbackProvider, sms.FallbackAfterMinutes, sms.DebtReminderEnabled, sms.ReceiptLinkEnabled,
                sms.PromotionEnabled, sms.ManualEnabled, sms.DebtReminderTemplate, sms.ReceiptLinkTemplate,
                sms.PromotionTemplate, sms.ManualTemplate, sms.SendReceiptOnSale, sms.TestMode, sms.TestAllowedNumbers,
                sms.DebtReminderStickyWaitMinutes, sms.ReceiptLinkStickyWaitMinutes,
                sms.PromotionStickyWaitMinutes, sms.ManualStickyWaitMinutes, sms.QuietHoursEnabled,
                sms.SendWindowStart, sms.SendWindowEnd),
            new NotificationSettingsDto(notif.Channels.Select(c => c.ToString()).ToList(), notif.CopyToAdmin, notif.PublicBaseUrl, notif.TelegramFormat.ToString(), notif.EmailFormat.ToString()));
    }
}
