using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Settings;
using Cartex.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.UI.ViewModels;

public partial class RemindersViewModel(
    ISettingsApi api,
    IToastService toast,
    IBusyService busy,
    AuthService auth) : ViewModelBase, ILoadable
{
    private SmsSettingsDto? _sms;

    [ObservableProperty] private bool _enabled;
    [ObservableProperty] private decimal _minDaysOverdue = 7;
    [ObservableProperty] private decimal _repeatEveryDays = 7;
    [ObservableProperty] private decimal _minBalance;
    [ObservableProperty] private decimal _sendHourLocal = 10;
    [ObservableProperty] private bool _notifyBeforeDue = true;
    [ObservableProperty] private decimal _daysBeforeDue = 1;
    [ObservableProperty] private bool _notifyOnDueDate = true;
    [ObservableProperty] private bool _channelTelegram;
    [ObservableProperty] private bool _channelSms;
    [ObservableProperty] private bool _channelEmail;
    [ObservableProperty] private string _overdueTemplate = string.Empty;
    [ObservableProperty] private string _dueSoonTemplate = string.Empty;
    [ObservableProperty] private string _dueTodayTemplate = string.Empty;
    [ObservableProperty] private bool _quietHoursEnabled = true;
    [ObservableProperty] private string _sendWindowStart = "09:00";
    [ObservableProperty] private string _sendWindowEnd = "21:00";
    [ObservableProperty] private bool _debtReminderEnabled = true;
    [ObservableProperty] private bool _receiptLinkEnabled;
    [ObservableProperty] private bool _promotionEnabled;
    [ObservableProperty] private bool _manualEnabled = true;
    [ObservableProperty] private bool _sendReceiptOnSale;
    [ObservableProperty] private string _debtReminderTemplate = string.Empty;
    [ObservableProperty] private string _receiptLinkTemplate = string.Empty;
    [ObservableProperty] private string _promotionTemplate = string.Empty;
    [ObservableProperty] private string _manualTemplate = string.Empty;

    public bool CanEditReminders => auth.HasPermission("notifications.edit");
    public bool CanEditSms => auth.HasPermission("settings.integrations");
    public bool CanEdit => CanEditReminders || CanEditSms;
    public IReadOnlyList<string> DebtVariables { get; } = ["{store}", "{name}", "{balance}", "{currency}"];
    /// Mijoz havolani ochmasdan ham asosiy raqamlarni ko'rishi uchun chek xabari
    /// summa, to'lov va qarz o'rinlarini ham qabul qiladi. Qarz yo'q savdoda
    /// {debt} va {due} bo'sh qoladi.
    public IReadOnlyList<string> ReceiptVariables { get; } =
        ["{store}", "{name}", "{link}", "{receipt}", "{date}", "{total}", "{paid}", "{debt}", "{due}"];
    public IReadOnlyList<string> PromotionVariables { get; } = ["{store}", "{name}", "{text}"];
    public IReadOnlyList<string> ManualVariables { get; } = ["{store}", "{name}", "{text}"];

    public async Task LoadAsync()
    {
        try
        {
            var reminderTask = api.GetReminderAsync();
            var settingsTask = api.GetAsync();
            await Task.WhenAll(reminderTask, settingsTask);
            ApplyReminder(reminderTask.Result);
            ApplySms(settingsTask.Result.Sms);
        }
        catch (Exception exception)
        {
            toast.Error(ApiErrors.Describe(exception));
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!CanEdit || _sms is null)
            return;
        try
        {
            using (busy.Begin(L["loading"]))
            {
                var tasks = new List<Task>();
                if (CanEditReminders)
                    tasks.Add(api.UpdateReminderAsync(BuildReminderRequest()));
                if (CanEditSms)
                    tasks.Add(api.UpdateSmsAsync(BuildSmsRequest(_sms)));
                await Task.WhenAll(tasks);
            }
            toast.Success(L["success"]);
        }
        catch (Exception exception)
        {
            toast.Error(ApiErrors.Describe(exception));
        }
    }

    private UpdateReminderSettingsRequest BuildReminderRequest()
    {
        var channels = new List<string>();
        if (ChannelTelegram) channels.Add("Telegram");
        if (ChannelSms) channels.Add("Sms");
        if (ChannelEmail) channels.Add("Email");
        return new UpdateReminderSettingsRequest(
            Enabled,
            (int)MinDaysOverdue,
            (int)RepeatEveryDays,
            MinBalance,
            (int)SendHourLocal,
            NotifyBeforeDue,
            (int)DaysBeforeDue,
            NotifyOnDueDate,
            channels,
            Empty(OverdueTemplate),
            Empty(DueSoonTemplate),
            Empty(DueTodayTemplate));
    }

    private UpdateSmsSettingsRequest BuildSmsRequest(SmsSettingsDto sms) => new(
        sms.Enabled,
        sms.Provider,
        sms.Login,
        null,
        sms.Sender,
        sms.BaseUrl,
        sms.FallbackProvider,
        sms.FallbackAfterMinutes,
        DebtReminderEnabled,
        ReceiptLinkEnabled,
        PromotionEnabled,
        ManualEnabled,
        Empty(DebtReminderTemplate),
        Empty(ReceiptLinkTemplate),
        Empty(PromotionTemplate),
        Empty(ManualTemplate),
        SendReceiptOnSale,
        sms.TestMode,
        sms.TestAllowedNumbers,
        sms.DebtReminderStickyWaitMinutes,
        sms.ReceiptLinkStickyWaitMinutes,
        sms.PromotionStickyWaitMinutes,
        sms.ManualStickyWaitMinutes,
        QuietHoursEnabled,
        SendWindowStart,
        SendWindowEnd);

    private void ApplyReminder(ReminderSettingsDto reminder)
    {
        Enabled = reminder.Enabled;
        MinDaysOverdue = reminder.MinDaysOverdue;
        RepeatEveryDays = reminder.RepeatEveryDays;
        MinBalance = reminder.MinBalance;
        SendHourLocal = reminder.SendHourLocal;
        NotifyBeforeDue = reminder.NotifyBeforeDue;
        DaysBeforeDue = reminder.DaysBeforeDue;
        NotifyOnDueDate = reminder.NotifyOnDueDate;
        ChannelTelegram = reminder.Channels.Contains("Telegram");
        ChannelSms = reminder.Channels.Contains("Sms");
        ChannelEmail = reminder.Channels.Contains("Email");
        OverdueTemplate = reminder.OverdueTemplate ?? string.Empty;
        DueSoonTemplate = reminder.DueSoonTemplate ?? string.Empty;
        DueTodayTemplate = reminder.DueTodayTemplate ?? string.Empty;
    }

    private void ApplySms(SmsSettingsDto sms)
    {
        _sms = sms;
        QuietHoursEnabled = sms.QuietHoursEnabled;
        SendWindowStart = sms.SendWindowStart;
        SendWindowEnd = sms.SendWindowEnd;
        DebtReminderEnabled = sms.DebtReminderEnabled;
        ReceiptLinkEnabled = sms.ReceiptLinkEnabled;
        PromotionEnabled = sms.PromotionEnabled;
        ManualEnabled = sms.ManualEnabled;
        SendReceiptOnSale = sms.SendReceiptOnSale;
        DebtReminderTemplate = sms.DebtReminderTemplate ?? string.Empty;
        ReceiptLinkTemplate = sms.ReceiptLinkTemplate ?? string.Empty;
        PromotionTemplate = sms.PromotionTemplate ?? string.Empty;
        ManualTemplate = sms.ManualTemplate ?? string.Empty;
    }

    private static string? Empty(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
