using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Application.Common.Messaging;

namespace Cartex.Application.Settings.Queries;

public record TelegramSettingsDto(bool Enabled, string? ChatId, bool HasBotToken);
public record EmailSettingsDto(bool Enabled, string? Host, int Port, bool UseSsl, string? Username, string? FromAddress, string? FromName, bool HasPassword);
public record SmsSettingsDto(bool Enabled, string Provider, string? Login, string? Sender, string? BaseUrl, bool HasPassword);
public record NotificationSettingsDto(List<string> Channels, bool CopyToAdmin, string? PublicBaseUrl, string TelegramFormat, string EmailFormat);

public record SettingsDto(TelegramSettingsDto Telegram, EmailSettingsDto Email, SmsSettingsDto Sms, NotificationSettingsDto Notification);

public record GetSettingsQuery : IRequest<SettingsDto>;

public sealed class GetSettingsQueryHandler(ISettingsService settings) : IRequestHandler<GetSettingsQuery, SettingsDto>
{
    public async Task<SettingsDto> Handle(GetSettingsQuery request, CancellationToken cancellationToken)
    {
        var tg = await settings.GetAsync<TelegramSettings>(SettingKeys.Telegram, cancellationToken) ?? new();
        var em = await settings.GetAsync<EmailSettings>(SettingKeys.Email, cancellationToken) ?? new();
        var sms = await settings.GetAsync<SmsSettings>(SettingKeys.Sms, cancellationToken) ?? new();
        var notif = await settings.GetAsync<NotificationSettings>(SettingKeys.Notification, cancellationToken) ?? new();

        return new SettingsDto(
            new TelegramSettingsDto(tg.Enabled, tg.ChatId, !string.IsNullOrEmpty(tg.BotToken)),
            new EmailSettingsDto(em.Enabled, em.Host, em.Port, em.UseSsl, em.Username, em.FromAddress, em.FromName, !string.IsNullOrEmpty(em.Password)),
            new SmsSettingsDto(sms.Enabled, sms.Provider, sms.Login, sms.Sender, sms.BaseUrl, !string.IsNullOrEmpty(sms.Password)),
            new NotificationSettingsDto(notif.Channels.Select(c => c.ToString()).ToList(), notif.CopyToAdmin, notif.PublicBaseUrl, notif.TelegramFormat.ToString(), notif.EmailFormat.ToString()));
    }
}
