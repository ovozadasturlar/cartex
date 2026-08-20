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

public record ReminderSettingsDto(
    bool Enabled,
    int MinDaysOverdue,
    int RepeatEveryDays,
    decimal MinBalance,
    int SendHourLocal,
    bool NotifyBeforeDue,
    int DaysBeforeDue,
    bool NotifyOnDueDate,
    List<string> Channels,
    string? OverdueTemplate = null,
    string? DueSoonTemplate = null,
    string? DueTodayTemplate = null);

public record UpdateReminderSettingsRequest(
    bool Enabled,
    int MinDaysOverdue,
    int RepeatEveryDays,
    decimal MinBalance,
    int SendHourLocal,
    bool NotifyBeforeDue,
    int DaysBeforeDue,
    bool NotifyOnDueDate,
    List<string> Channels,
    string? OverdueTemplate = null,
    string? DueSoonTemplate = null,
    string? DueTodayTemplate = null);
public record TelegramTestRequest(string? BotToken);
public record TelegramTestResult(bool Ok, string? BotUsername);
public record SendTestMessageRequest(string Channel, string? Recipient);
public record ReceiptSettingsDto(
    string? HeaderText,
    string? FooterText,
    int PaperWidth,
    string PaperFormat = "Thermal",
    bool ShowBusinessName = true,
    bool ShowBranchName = true,
    bool ShowAddress = true,
    bool ShowPhone = true,
    bool ShowCashier = true,
    bool ShowCustomer = true,
    bool ShowReceiptNumber = true,
    bool ShowPaymentDetails = true,
    bool ShowQrCode = true,
    bool ShowElectronicLink = true,
    string? PublicReceiptBaseUrl = null,
    bool ShowLogo = true,
    bool ShowCustomerPhone = true,
    bool ShowCustomerEmail = false);
public record UpdateReceiptSettingsRequest(
    string? HeaderText,
    string? FooterText,
    int PaperWidth,
    string PaperFormat = "Thermal",
    bool ShowBusinessName = true,
    bool ShowBranchName = true,
    bool ShowAddress = true,
    bool ShowPhone = true,
    bool ShowCashier = true,
    bool ShowCustomer = true,
    bool ShowReceiptNumber = true,
    bool ShowPaymentDetails = true,
    bool ShowQrCode = true,
    bool ShowElectronicLink = true,
    bool ShowLogo = true,
    bool ShowCustomerPhone = true,
    bool ShowCustomerEmail = false);
public record BarcodeLabelSettingsDto(
    bool DefaultWithPrice = false,
    bool AllowPriceOverride = true,
    bool ShowSku = false,
    int NameLines = 2,
    string CurrencyDisplay = "symbol",
    string CurrencyCase = "original",
    string PriceCurrencyMode = "product");
public record UpdateBarcodeLabelSettingsRequest(
    bool DefaultWithPrice,
    bool AllowPriceOverride,
    bool ShowSku,
    int NameLines,
    string CurrencyDisplay,
    string CurrencyCase,
    string PriceCurrencyMode);
public record ProformaSettingsDto(
    string? HeaderText = null,
    string? FooterText = null,
    int PaperWidth = 32,
    string PaperFormat = "Thermal",
    bool ShowBusinessName = true,
    bool ShowAddress = true,
    bool ShowPhone = true,
    bool ShowSeller = true,
    bool ShowCustomer = true,
    bool ShowNote = true,
    bool ShowCartCode = true);
public record UpdateProformaSettingsRequest(
    string? HeaderText,
    string? FooterText,
    int PaperWidth,
    string PaperFormat = "Thermal",
    bool ShowBusinessName = true,
    bool ShowAddress = true,
    bool ShowPhone = true,
    bool ShowSeller = true,
    bool ShowCustomer = true,
    bool ShowNote = true,
    bool ShowCartCode = true);
/// Savdo siyosati bitta hujjat: GET ham, PUT ham aynan shu shaklni ishlatadi. Ilgari bu ro'yxat
/// to'rt joyda pozitsion record bo'lib takrorlangan edi — yangi maydon to'rtta imzoga bir xil
/// tartibda qo'shilishi kerak edi, aks holda sozlama jimgina yo'qolardi.
public sealed record SalesPolicyDto
{
    public string ShiftPolicy { get; init; } = "CashOnly";
    public decimal? MaxDiscountPercent { get; init; }
    public decimal? MaxDebtWriteOffAmount { get; init; }
    public decimal? MaxDebtWriteOffPercent { get; init; }
    public decimal DefaultMinStock { get; init; }
    public int StaleRateDays { get; init; } = 3;
    public bool AllowDebtSales { get; init; } = true;
    public bool AllowCustomerCredit { get; init; }
    public bool RequireDebtDueDate { get; init; } = true;
    public bool RequireSupplier { get; init; }
    public bool ShowOutOfStock { get; init; }
    public bool ShowUnlistedProducts { get; init; } = true;
    public bool AllowInsufficientStockSales { get; init; }
    public bool AllowNegativeStockWhenOffline { get; init; }
    public bool AllowRetroactiveCashback { get; init; }
    public string SaleCorrectionWindow { get; init; } = "Shift";
    public int SaleCorrectionDays { get; init; } = 1;
    public bool AllowDebtWriteOff { get; init; } = true;
    public bool PrintMoneyDocuments { get; init; } = true;
    public bool PrintCartProforma { get; init; } = true;
    public bool AllowConsolidatedAct { get; init; } = true;
    public bool AllowCustomerLoans { get; init; }
    public decimal? MaxCustomerLoan { get; init; }
    public bool UpdateCatalogPriceOnSale { get; init; } = true;
    public decimal? MaxPriceIncreasePercent { get; init; }
    public string CustomerRequirement { get; init; } = "OnDebt";
    public bool AllowReturnOnVoidedSale { get; init; }
    public bool AllowFreeReturnLines { get; init; } = true;
    public bool RequireReturnReason { get; init; }
    public bool AllowSaleQueue { get; init; } = true;
}


public record LoginMethodsSettingsDto(bool QrEnabled, int QrRefreshSeconds, bool KeyEnabled);
public record UpdateLoginMethodsRequest(bool QrEnabled, int QrRefreshSeconds, bool KeyEnabled);
public record StorageSettingsDto(bool Enabled, string Provider, string? Endpoint, string? AccessKey, string? Bucket, bool UseSsl, bool HasSecretKey, int SecretKeyLength = 0);
public record StartStorageMigrationRequest(string Direction);
public record StorageMigrationStatusDto(bool Running, string? Direction, int Processed, int Failed, int Total, string? Error, DateTime? FinishedAt);
public record UpdateStorageSettingsRequest(bool Enabled, string Provider, string? Endpoint, string? AccessKey, string? SecretKey, string? Bucket, bool UseSsl);
public record CloudBridgeSettingsDto(bool Enabled, string? GatewayUrl, bool HasLicenseKey);
public record UpdateCloudBridgeSettingsRequest(bool Enabled, string? GatewayUrl, string? LicenseKey);
