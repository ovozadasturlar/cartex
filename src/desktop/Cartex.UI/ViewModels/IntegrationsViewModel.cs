using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Settings;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public sealed record SmtpPreset(string Name, string? Host, int Port, bool Ssl);

public partial class IntegrationsViewModel(ISettingsApi api, IToastService toast, IBusyService busy) : ViewModelBase, ILoadable
{
    private SmsSettingsDto? _smsSettings;
    public ObservableCollection<string> SmsProviders { get; } = ["eskiz", "playmobile"];

    public ObservableCollection<SmtpPreset> SmtpPresets { get; } =
    [
        new("Gmail", "smtp.gmail.com", 587, true),
        new("Yandex", "smtp.yandex.ru", 465, true),
        new("Mail.ru", "smtp.mail.ru", 465, true),
        new("Outlook", "smtp.office365.com", 587, true),
        new("Yahoo", "smtp.mail.yahoo.com", 465, true),
        new("Boshqa", null, 587, true),
    ];

    [ObservableProperty] private string _selectedSectionKey = "telegram";
    public bool IsTelegram => SelectedSectionKey == "telegram";
    public bool IsEmail => SelectedSectionKey == "email";
    public bool IsSms => SelectedSectionKey == "sms";
    public bool IsReceipt => SelectedSectionKey == "receipt";
    public bool IsStorage => SelectedSectionKey == "storage";
    public bool IsCloudBridge => SelectedSectionKey == "cloudBridge";

    partial void OnSelectedSectionKeyChanged(string value)
    {
        OnPropertyChanged(nameof(IsTelegram));
        OnPropertyChanged(nameof(IsEmail));
        OnPropertyChanged(nameof(IsSms));
        OnPropertyChanged(nameof(IsReceipt));
        OnPropertyChanged(nameof(IsStorage));
        OnPropertyChanged(nameof(IsCloudBridge));
    }

    [RelayCommand]
    private void SelectSection(string key) => SelectedSectionKey = key;

    [ObservableProperty] private bool _telegramEnabled;
    [ObservableProperty] private string? _telegramChatId;
    [ObservableProperty] private string? _telegramBotToken;
    [ObservableProperty] private bool _telegramHasToken;
    [ObservableProperty] private string? _telegramTokenMask;
    [ObservableProperty] private string? _telegramStatus;

    [ObservableProperty] private bool _emailEnabled;
    [ObservableProperty] private SmtpPreset? _selectedSmtpPreset;
    [ObservableProperty] private string? _emailHost;
    [ObservableProperty] private int _emailPort = 587;
    [ObservableProperty] private bool _emailUseSsl = true;
    [ObservableProperty] private string? _emailUsername;
    [ObservableProperty] private string? _emailPassword;
    [ObservableProperty] private string? _emailFromAddress;
    [ObservableProperty] private string? _emailFromName;
    [ObservableProperty] private bool _emailHasPassword;

    [ObservableProperty] private bool _smsEnabled;
    [ObservableProperty] private string _smsProvider = "eskiz";
    [ObservableProperty] private string? _smsLogin;
    [ObservableProperty] private string? _smsPassword;
    [ObservableProperty] private string? _smsSender;
    [ObservableProperty] private string? _smsBaseUrl;
    [ObservableProperty] private bool _smsHasPassword;

    [ObservableProperty] private string? _emailTestRecipient;
    [ObservableProperty] private string? _smsTestRecipient;

    [ObservableProperty] private bool _channelTelegram;
    [ObservableProperty] private bool _channelSms;
    [ObservableProperty] private bool _channelEmail;
    [ObservableProperty] private bool _copyToAdmin;
    [ObservableProperty] private string? _publicBaseUrl;
    [ObservableProperty] private string _telegramFormat = "Auto";
    [ObservableProperty] private bool _storageEnabled;
    [ObservableProperty] private int _storageProviderIndex;
    [ObservableProperty] private string? _storageEndpoint;
    [ObservableProperty] private string? _storageAccessKey;
    [ObservableProperty] private string? _storageSecretKey;
    [ObservableProperty] private string? _storageBucket;
    [ObservableProperty] private bool _storageUseSsl;
    [ObservableProperty] private bool _storageHasSecret;
    [ObservableProperty] private string? _storageSecretPlaceholder;
    [ObservableProperty] private bool _cloudBridgeEnabled;
    [ObservableProperty] private string? _cloudBridgeGatewayUrl;
    [ObservableProperty] private string? _cloudBridgeLicenseKey;
    [ObservableProperty] private bool _cloudBridgeHasLicense;
    [ObservableProperty] private string _emailFormat = "Auto";

    public string[] ReceiptFormats { get; } = ["Auto", "Link", "Pdf", "Text"];

    public async Task LoadAsync()
    {
        try
        {
            using (busy.Begin(L["loading"]))
            {
                var s = await api.GetAsync();

                TelegramEnabled = s.Telegram.Enabled;
                TelegramChatId = s.Telegram.ChatId;
                TelegramHasToken = s.Telegram.HasBotToken;
                TelegramTokenMask = s.Telegram.BotTokenLength > 0 ? new string('•', s.Telegram.BotTokenLength) : null;
                TelegramBotToken = null;
                TelegramStatus = null;

                EmailEnabled = s.Email.Enabled;
                EmailHost = s.Email.Host;
                EmailPort = s.Email.Port;
                EmailUseSsl = s.Email.UseSsl;
                EmailUsername = s.Email.Username;
                EmailFromAddress = s.Email.FromAddress;
                EmailFromName = s.Email.FromName;
                EmailHasPassword = s.Email.HasPassword;
                EmailPassword = null;

                SmsEnabled = s.Sms.Enabled;
                SmsProvider = s.Sms.Provider;
                SmsLogin = s.Sms.Login;
                SmsSender = s.Sms.Sender;
                SmsBaseUrl = s.Sms.BaseUrl;
                SmsHasPassword = s.Sms.HasPassword;
                SmsPassword = null;
                _smsSettings = s.Sms;

                ChannelTelegram = s.Notification.Channels.Contains("Telegram");
                ChannelSms = s.Notification.Channels.Contains("Sms");
                ChannelEmail = s.Notification.Channels.Contains("Email");
                CopyToAdmin = s.Notification.CopyToAdmin;
                PublicBaseUrl = s.Notification.PublicBaseUrl;
                TelegramFormat = s.Notification.TelegramFormat;
                EmailFormat = s.Notification.EmailFormat;

                var storage = await api.GetStorageAsync();
                StorageEnabled = storage.Enabled;
                StorageProviderIndex = storage.Provider == "minio" ? 1 : 0;
                StorageEndpoint = storage.Endpoint;
                StorageAccessKey = storage.AccessKey;
                StorageBucket = storage.Bucket;
                StorageUseSsl = storage.UseSsl;
                StorageHasSecret = storage.HasSecretKey;
                StorageSecretKey = null;
                StorageSecretPlaceholder = storage.SecretKeyLength > 0
                    ? new string('•', storage.SecretKeyLength)
                    : L["secret_keep_blank"];
                _ = RefreshMigrationStateAsync();

                var bridge = await api.GetCloudBridgeAsync();
                CloudBridgeEnabled = bridge.Enabled;
                CloudBridgeGatewayUrl = bridge.GatewayUrl;
                CloudBridgeHasLicense = bridge.HasLicenseKey;
                CloudBridgeLicenseKey = null;
            }
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
    }

    partial void OnSelectedSmtpPresetChanged(SmtpPreset? value)
    {
        if (value?.Host is null) return;
        EmailHost = value.Host;
        EmailPort = value.Port;
        EmailUseSsl = value.Ssl;
    }

    public bool StorageIsMinio => StorageProviderIndex == 1;

    partial void OnStorageProviderIndexChanged(int value) => OnPropertyChanged(nameof(StorageIsMinio));

    partial void OnSmsProviderChanged(string value) =>
        SmsBaseUrl = value switch
        {
            "eskiz" => "https://notify.eskiz.uz/api",
            "playmobile" => "https://send.smsxabar.uz",
            _ => SmsBaseUrl
        };

    [RelayCommand]
    private async Task ConnectTelegramAsync()
    {
        if (string.IsNullOrWhiteSpace(TelegramBotToken) && !TelegramHasToken)
        {
            toast.Warning(L["bot_token_required"]);
            return;
        }
        try
        {
            using (busy.Begin(L["loading"]))
            {
                var result = await api.TestTelegramAsync(new TelegramTestRequest(TelegramBotToken));
                if (!result.Ok)
                {
                    TelegramStatus = "✕";
                    toast.Warning(L["error"]);
                    return;
                }
                await api.UpdateTelegramAsync(new UpdateTelegramSettingsRequest(true, TelegramChatId, TelegramBotToken));
                await LoadAsync();
                TelegramStatus = $"✓ @{result.BotUsername}";
                toast.Success(L["success"]);
            }
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task DisconnectTelegramAsync()
    {
        try
        {
            using (busy.Begin(L["loading"]))
            {
                await api.UpdateTelegramAsync(new UpdateTelegramSettingsRequest(false, TelegramChatId, null));
                await LoadAsync();
            }
            toast.Success(L["success"]);
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private Task TestTelegramMessageAsync() => SendTestAsync("telegram", TelegramChatId);

    [RelayCommand]
    private Task TestEmailMessageAsync() => SendTestAsync("email", EmailTestRecipient);

    [RelayCommand]
    private Task TestSmsMessageAsync() => SendTestAsync("sms", SmsTestRecipient);

    private async Task SendTestAsync(string channel, string? recipient)
    {
        try
        {
            using (busy.Begin(L["loading"]))
                await api.SendTestMessageAsync(new SendTestMessageRequest(channel, recipient));
            toast.Success(L["test_sent"]);
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task SaveTelegramAsync()
    {
        try
        {
            using (busy.Begin(L["loading"]))
                await api.UpdateTelegramAsync(new UpdateTelegramSettingsRequest(TelegramEnabled, TelegramChatId, TelegramBotToken));
            toast.Success(L["success"]);
            await LoadAsync();
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task SaveEmailAsync()
    {
        try
        {
            using (busy.Begin(L["loading"]))
                await api.UpdateEmailAsync(new UpdateEmailSettingsRequest(EmailEnabled, EmailHost, EmailPort, EmailUseSsl, EmailUsername, EmailPassword, EmailFromAddress, EmailFromName));
            toast.Success(L["success"]);
            await LoadAsync();
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task SaveSmsAsync()
    {
        try
        {
            using (busy.Begin(L["loading"]))
            {
                var sms = _smsSettings ?? new SmsSettingsDto(false, "device", null, null, null, false);
                await api.UpdateSmsAsync(new UpdateSmsSettingsRequest(
                    SmsEnabled, SmsProvider, SmsLogin, SmsPassword, SmsSender, SmsBaseUrl,
                    sms.FallbackProvider, sms.FallbackAfterMinutes, sms.DebtReminderEnabled,
                    sms.ReceiptLinkEnabled, sms.PromotionEnabled, sms.ManualEnabled,
                    sms.DebtReminderTemplate, sms.ReceiptLinkTemplate, sms.PromotionTemplate,
                    sms.ManualTemplate, sms.SendReceiptOnSale, sms.TestMode, sms.TestAllowedNumbers));
            }
            toast.Success(L["success"]);
            await LoadAsync();
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task SaveStorageAsync()
    {
        try
        {
            using (busy.Begin(L["loading"]))
                await api.UpdateStorageAsync(new UpdateStorageSettingsRequest(StorageEnabled, StorageProviderIndex == 1 ? "minio" : "local", StorageEndpoint, StorageAccessKey, StorageSecretKey, StorageBucket, StorageUseSsl));
            toast.Success(L["success"]);
            await LoadAsync();
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
    }

    [ObservableProperty] private bool _migrationRunning;
    [ObservableProperty] private string? _migrationText;
    [ObservableProperty] private double _migrationProgress;
    private Avalonia.Threading.DispatcherTimer? _migrationTimer;

    [RelayCommand]
    private Task MigrateToRemoteAsync() => StartMigrationAsync("LocalToRemote");

    [RelayCommand]
    private Task MigrateToLocalAsync() => StartMigrationAsync("RemoteToLocal");

    private async Task StartMigrationAsync(string direction)
    {
        try
        {
            await api.StartStorageMigrationAsync(new StartStorageMigrationRequest(direction));
            MigrationRunning = true;
            MigrationText = "…";
            StartMigrationPolling();
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
    }

    private void StartMigrationPolling()
    {
        _migrationTimer ??= new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _migrationTimer.Tick -= OnMigrationTick;
        _migrationTimer.Tick += OnMigrationTick;
        _migrationTimer.Start();
    }

    private async void OnMigrationTick(object? sender, EventArgs e)
    {
        try
        {
            var s = await api.GetStorageMigrationAsync();
            MigrationRunning = s.Running;
            MigrationText = $"{s.Processed}/{s.Total}";
            MigrationProgress = s.Total > 0 ? s.Processed * 100.0 / s.Total : 0;
            if (s.Running) return;
            _migrationTimer?.Stop();
            if (s.Error is not null) toast.Error(s.Error);
            else toast.Success(string.Format(L["migrate_storage_done_fmt"], s.Processed, s.Failed));
        }
        catch { _migrationTimer?.Stop(); MigrationRunning = false; }
    }

    private async Task RefreshMigrationStateAsync()
    {
        try
        {
            var s = await api.GetStorageMigrationAsync();
            if (!s.Running) return;
            MigrationRunning = true;
            MigrationText = $"{s.Processed}/{s.Total}";
            MigrationProgress = s.Total > 0 ? s.Processed * 100.0 / s.Total : 0;
            StartMigrationPolling();
        }
        catch { }
    }

    [RelayCommand]
    private async Task SaveCloudBridgeAsync()
    {
        try
        {
            using (busy.Begin(L["loading"]))
                await api.UpdateCloudBridgeAsync(new UpdateCloudBridgeSettingsRequest(CloudBridgeEnabled, CloudBridgeGatewayUrl, CloudBridgeLicenseKey));
            toast.Success(L["success"]);
            await LoadAsync();
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task SaveNotificationAsync()
    {
        try
        {
            var channels = new List<string>();
            if (ChannelTelegram) channels.Add("Telegram");
            if (ChannelSms) channels.Add("Sms");
            if (ChannelEmail) channels.Add("Email");

            using (busy.Begin(L["loading"]))
                await api.UpdateNotificationAsync(new UpdateNotificationSettingsRequest(channels, CopyToAdmin, PublicBaseUrl, TelegramFormat, EmailFormat));
            toast.Success(L["success"]);
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
    }
}
