namespace Cartex.Shared.Models.Settings;

public record TelegramSettingsDto(bool Enabled, string? ChatId, bool HasBotToken);
public record EmailSettingsDto(bool Enabled, string? Host, int Port, bool UseSsl, string? Username, string? FromAddress, string? FromName, bool HasPassword);
public record SmsSettingsDto(bool Enabled, string Provider, string? Login, string? Sender, string? BaseUrl, bool HasPassword);
public record NotificationSettingsDto(List<string> Channels, bool CopyToAdmin, string? PublicBaseUrl, string TelegramFormat, string EmailFormat);
public record SettingsDto(TelegramSettingsDto Telegram, EmailSettingsDto Email, SmsSettingsDto Sms, NotificationSettingsDto Notification);

public record UpdateTelegramSettingsRequest(bool Enabled, string? ChatId, string? BotToken);
public record UpdateEmailSettingsRequest(bool Enabled, string? Host, int Port, bool UseSsl, string? Username, string? Password, string? FromAddress, string? FromName);
public record UpdateSmsSettingsRequest(bool Enabled, string Provider, string? Login, string? Password, string? Sender, string? BaseUrl);
public record UpdateNotificationSettingsRequest(List<string> Channels, bool CopyToAdmin, string? PublicBaseUrl, string? TelegramFormat = null, string? EmailFormat = null);

public record ReminderSettingsDto(bool Enabled, int MinDaysOverdue, int RepeatEveryDays, decimal MinBalance, int SendHourLocal, List<string> Channels);

public record UpdateReminderSettingsRequest(bool Enabled, int MinDaysOverdue, int RepeatEveryDays, decimal MinBalance, int SendHourLocal, List<string> Channels);
public record TelegramTestRequest(string? BotToken);
public record TelegramTestResult(bool Ok, string? BotUsername);
public record SendTestMessageRequest(string Channel, string? Recipient);
