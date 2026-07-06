using Cartex.Shared.Models.Settings;
using Refit;

namespace Cartex.ApiClient.Api;

public interface ISettingsApi
{
    [Get("/api/settings")]
    Task<SettingsDto> GetAsync();

    [Put("/api/settings/telegram")]
    Task UpdateTelegramAsync([Body] UpdateTelegramSettingsRequest request);

    [Post("/api/settings/telegram/test")]
    Task<TelegramTestResult> TestTelegramAsync([Body] TelegramTestRequest request);

    [Post("/api/settings/integrations/test")]
    Task SendTestMessageAsync([Body] SendTestMessageRequest request);

    [Put("/api/settings/email")]
    Task UpdateEmailAsync([Body] UpdateEmailSettingsRequest request);

    [Put("/api/settings/sms")]
    Task UpdateSmsAsync([Body] UpdateSmsSettingsRequest request);

    [Put("/api/settings/notification")]
    Task UpdateNotificationAsync([Body] UpdateNotificationSettingsRequest request);

    [Get("/api/settings/reminder")]
    Task<ReminderSettingsDto> GetReminderAsync();

    [Put("/api/settings/reminder")]
    Task UpdateReminderAsync([Body] UpdateReminderSettingsRequest request);
}
