using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Settings;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class RemindersViewModel(ISettingsApi api, IToastService toast, IBusyService busy) : ViewModelBase, ILoadable
{
    [ObservableProperty] private bool _enabled;
    [ObservableProperty] private decimal _minDaysOverdue = 7;
    [ObservableProperty] private decimal _repeatEveryDays = 7;
    [ObservableProperty] private decimal _minBalance;
    [ObservableProperty] private decimal _sendHourLocal = 10;
    [ObservableProperty] private bool _channelTelegram;
    [ObservableProperty] private bool _channelSms;
    [ObservableProperty] private bool _channelEmail;

    public async Task LoadAsync()
    {
        try
        {
            var cfg = await api.GetReminderAsync();
            Enabled = cfg.Enabled;
            MinDaysOverdue = cfg.MinDaysOverdue;
            RepeatEveryDays = cfg.RepeatEveryDays;
            MinBalance = cfg.MinBalance;
            SendHourLocal = cfg.SendHourLocal;
            ChannelTelegram = cfg.Channels.Contains("Telegram");
            ChannelSms = cfg.Channels.Contains("Sms");
            ChannelEmail = cfg.Channels.Contains("Email");
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            var channels = new List<string>();
            if (ChannelTelegram) channels.Add("Telegram");
            if (ChannelSms) channels.Add("Sms");
            if (ChannelEmail) channels.Add("Email");

            using (busy.Begin(L["loading"]))
                await api.UpdateReminderAsync(new UpdateReminderSettingsRequest(Enabled, (int)MinDaysOverdue, (int)RepeatEveryDays, MinBalance, (int)SendHourLocal, channels));
            toast.Success(L["success"]);
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
    }
}
