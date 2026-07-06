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

    partial void OnSelectedSectionKeyChanged(string value)
    {
        OnPropertyChanged(nameof(IsTelegram));
        OnPropertyChanged(nameof(IsEmail));
        OnPropertyChanged(nameof(IsSms));
        OnPropertyChanged(nameof(IsReceipt));
    }

    [RelayCommand]
    private void SelectSection(string key) => SelectedSectionKey = key;

    [ObservableProperty] private bool _telegramEnabled;
    [ObservableProperty] private string? _telegramChatId;
    [ObservableProperty] private string? _telegramBotToken;
    [ObservableProperty] private bool _telegramHasToken;
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
    [ObservableProperty] private string _emailFormat = "Auto";

    public string[] ReceiptFormats { get; } = ["Auto", "Link", "Pdf"];

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

                ChannelTelegram = s.Notification.Channels.Contains("Telegram");
                ChannelSms = s.Notification.Channels.Contains("Sms");
                ChannelEmail = s.Notification.Channels.Contains("Email");
                CopyToAdmin = s.Notification.CopyToAdmin;
                PublicBaseUrl = s.Notification.PublicBaseUrl;
                TelegramFormat = s.Notification.TelegramFormat;
                EmailFormat = s.Notification.EmailFormat;
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
        try
        {
            using (busy.Begin(L["loading"]))
            {
                var result = await api.TestTelegramAsync(new TelegramTestRequest(TelegramBotToken));
                TelegramStatus = result.Ok ? $"✓ @{result.BotUsername}" : "✕";
                if (result.Ok) toast.Success(L["success"]); else toast.Warning(L["error"]);
            }
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
                await api.UpdateSmsAsync(new UpdateSmsSettingsRequest(SmsEnabled, SmsProvider, SmsLogin, SmsPassword, SmsSender, SmsBaseUrl));
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
