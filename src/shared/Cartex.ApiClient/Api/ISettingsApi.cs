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

    [Get("/api/settings/receipt")]
    Task<ReceiptSettingsDto> GetReceiptAsync();

    [Put("/api/settings/receipt")]
    Task UpdateReceiptAsync([Body] UpdateReceiptSettingsRequest request);

    [Get("/api/settings/barcode-label")]
    Task<BarcodeLabelSettingsDto> GetBarcodeLabelAsync();

    [Put("/api/settings/barcode-label")]
    Task UpdateBarcodeLabelAsync([Body] UpdateBarcodeLabelSettingsRequest request);

    [Get("/api/settings/sales-policy")]
    Task<SalesPolicyDto> GetSalesPolicyAsync();

    [Put("/api/settings/sales-policy")]
    Task UpdateSalesPolicyAsync([Body] UpdateSalesPolicyRequest request);

    [Get("/api/settings/login-methods")]
    Task<LoginMethodsSettingsDto> GetLoginMethodsAsync();

    [Put("/api/settings/login-methods")]
    Task UpdateLoginMethodsAsync([Body] UpdateLoginMethodsRequest request);

    [Get("/api/settings/storage")]
    Task<StorageSettingsDto> GetStorageAsync();

    [Put("/api/settings/storage")]
    Task UpdateStorageAsync([Body] UpdateStorageSettingsRequest request);

    [Post("/api/settings/storage/migrate")]
    Task StartStorageMigrationAsync([Body] StartStorageMigrationRequest request);

    [Get("/api/settings/storage/migrate")]
    Task<StorageMigrationStatusDto> GetStorageMigrationAsync();

    [Get("/api/settings/reminder")]
    Task<ReminderSettingsDto> GetReminderAsync();

    [Put("/api/settings/reminder")]
    Task UpdateReminderAsync([Body] UpdateReminderSettingsRequest request);

    [Get("/api/settings/cloud-bridge")]
    Task<CloudBridgeSettingsDto> GetCloudBridgeAsync();

    [Put("/api/settings/cloud-bridge")]
    Task UpdateCloudBridgeAsync([Body] UpdateCloudBridgeSettingsRequest request);
}
