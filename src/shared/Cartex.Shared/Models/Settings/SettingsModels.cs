namespace Cartex.Shared.Models.Settings;

public record TelegramSettingsDto(bool Enabled, string? ChatId, bool HasBotToken, int BotTokenLength);
public record EmailSettingsDto(bool Enabled, string? Host, int Port, bool UseSsl, string? Username, string? FromAddress, string? FromName, bool HasPassword);
public record SmsSettingsDto(bool Enabled, string Provider, string? Login, string? Sender, string? BaseUrl, bool HasPassword);
public record NotificationSettingsDto(List<string> Channels, bool CopyToAdmin, string? PublicBaseUrl, string TelegramFormat, string EmailFormat);
public record SettingsDto(TelegramSettingsDto Telegram, EmailSettingsDto Email, SmsSettingsDto Sms, NotificationSettingsDto Notification);

public record UpdateTelegramSettingsRequest(bool Enabled, string? ChatId, string? BotToken, bool ClearToken = false);
public record UpdateEmailSettingsRequest(bool Enabled, string? Host, int Port, bool UseSsl, string? Username, string? Password, string? FromAddress, string? FromName);
public record UpdateSmsSettingsRequest(bool Enabled, string Provider, string? Login, string? Password, string? Sender, string? BaseUrl);
public record UpdateNotificationSettingsRequest(List<string> Channels, bool CopyToAdmin, string? PublicBaseUrl, string? TelegramFormat = null, string? EmailFormat = null);

public record ReminderSettingsDto(bool Enabled, int MinDaysOverdue, int RepeatEveryDays, decimal MinBalance, int SendHourLocal, List<string> Channels, string? OverdueTemplate = null, string? DueSoonTemplate = null);

public record UpdateReminderSettingsRequest(bool Enabled, int MinDaysOverdue, int RepeatEveryDays, decimal MinBalance, int SendHourLocal, List<string> Channels, string? OverdueTemplate = null, string? DueSoonTemplate = null);
public record TelegramTestRequest(string? BotToken);
public record TelegramTestResult(bool Ok, string? BotUsername);
public record SendTestMessageRequest(string Channel, string? Recipient);
public record ReceiptSettingsDto(string? HeaderText, string? FooterText, int PaperWidth);
public record UpdateReceiptSettingsRequest(string? HeaderText, string? FooterText, int PaperWidth);
public record SalesPolicyDto(string ShiftPolicy, decimal MaxDiscountPercent, decimal DefaultMinStock, int StaleRateDays);
public record UpdateSalesPolicyRequest(string ShiftPolicy, decimal MaxDiscountPercent, decimal DefaultMinStock, int StaleRateDays);
public record LoginMethodsSettingsDto(bool QrEnabled, int QrRefreshSeconds, bool KeyEnabled);
public record UpdateLoginMethodsRequest(bool QrEnabled, int QrRefreshSeconds, bool KeyEnabled);
public record StorageSettingsDto(bool Enabled, string Provider, string? Endpoint, string? AccessKey, string? Bucket, bool UseSsl, bool HasSecretKey);
public record UpdateStorageSettingsRequest(bool Enabled, string Provider, string? Endpoint, string? AccessKey, string? SecretKey, string? Bucket, bool UseSsl);
public record CloudBridgeSettingsDto(bool Enabled, string? GatewayUrl, bool HasLicenseKey);
public record UpdateCloudBridgeSettingsRequest(bool Enabled, string? GatewayUrl, string? LicenseKey);
