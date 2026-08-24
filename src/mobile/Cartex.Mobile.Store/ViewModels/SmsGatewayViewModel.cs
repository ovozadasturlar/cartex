using System.Collections.ObjectModel;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;
using Cartex.Shared.Models.SmsGateway;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.Mobile.Store.ViewModels;

public partial class SmsGatewayViewModel(SmsGatewayHostService host, ISmsGatewayPlatform platform) : ObservableObject
{
    public ObservableCollection<MobileSimInfo> Sims { get; } = [];
    public ObservableCollection<MobileSmsGatewayCard> Devices { get; } = [];
    public ObservableCollection<SmsGatewayJobDto> Jobs { get; } = [];
    private readonly Dictionary<int, SmsGatewayHostStateDto> _states = [];

    [ObservableProperty] private MobileSimInfo? _selectedSim;
    [ObservableProperty] private string _phoneLabel = string.Empty;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private string _statusReason = string.Empty;
    [ObservableProperty] private bool _permissionGranted;
    [ObservableProperty] private bool _isRegistered;
    [ObservableProperty] private bool _isTrusted;
    [ObservableProperty] private bool _isConsented;
    [ObservableProperty] private bool _isPaused;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isUnlimited;
    [ObservableProperty] private string _monthlyQuota = string.Empty;
    [ObservableProperty] private int _quotaResetDay = 1;
    [ObservableProperty] private int _maxPerHour = 60;
    [ObservableProperty] private int _minIntervalSeconds = 4;
    [ObservableProperty] private int _lowQuotaWarnPercent = 10;
    [ObservableProperty] private string _quotaText = string.Empty;
    [ObservableProperty] private string _quotaDetail = string.Empty;
    [ObservableProperty] private double _quotaProgress;
    [ObservableProperty] private bool _showQuotaProgress;
    [ObservableProperty] private bool _isQuotaSuccess;
    [ObservableProperty] private bool _isQuotaWarning;
    [ObservableProperty] private bool _isQuotaOrange;
    [ObservableProperty] private bool _isQuotaDanger;
    [ObservableProperty] private bool _isQuotaExhausted;
    [ObservableProperty] private bool _isOverQuota;
    [ObservableProperty] private bool _testMode = true;
    [ObservableProperty] private string _messageTypes = string.Empty;
    [ObservableProperty] private string _testPhone = string.Empty;
    [ObservableProperty] private long _deviceId;
    [ObservableProperty] private int _simSlot;

    public bool IsUnregistered => !IsRegistered;
    public bool IsLimitEnabled => !IsUnlimited;
    public string PauseAction => Loc.Instance[IsPaused ? "sms_gateway_resume" : "sms_gateway_pause"];

    partial void OnIsRegisteredChanged(bool value) => OnPropertyChanged(nameof(IsUnregistered));
    partial void OnIsPausedChanged(bool value) => OnPropertyChanged(nameof(PauseAction));
    partial void OnSelectedSimChanged(MobileSimInfo? value)
    {
        if (value is null)
            return;
        Preferences.Set("sms_gateway_selected_slot", value.Slot);
        if (_states.TryGetValue(value.Slot, out var state))
            ApplyState(state);
        else
            ResetDevice(value.Slot);
    }
    partial void OnIsUnlimitedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsLimitEnabled));
        if (value)
            MonthlyQuota = string.Empty;
    }

    public async Task AppearAsync()
    {
        IsBusy = true;
        try
        {
            PermissionGranted = await platform.EnsurePermissionAsync();
            if (!PermissionGranted)
            {
                Status = Loc.Instance["sms_gateway_permission_required"];
                return;
            }
            ReplaceSims(await platform.GetSimsAsync());
            if (SelectedSim is null)
            {
                Status = Loc.Instance["sms_gateway_no_sim"];
                return;
            }
            await LoadStateAsync();
            if (!IsRegistered)
                Status = Loc.Instance["sms_gateway_register_hint"];
        }
        catch (Exception exception)
        {
            Status = Describe(exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// Birinchi ekran faqat holatni ko'rsatadi; sozlamalar, sinov xabari va rozilikni
    /// bekor qilish alohida sahifada — shunda kundalik ish uchun ochilgan sahifa toza qoladi.
    [RelayCommand]
    private Task OpenSettingsAsync() => Shell.Current.GoToAsync("sms-gateway-settings");

    [RelayCommand]
    private async Task RegisterAsync()
    {
        if (SelectedSim is null)
        {
            Status = Loc.Instance["sms_gateway_no_sim"];
            return;
        }
        if (!TryBuildScope(out var scope))
            return;
        if (!await ConfirmScopeAsync(scope))
            return;
        await RunAsync(async () =>
        {
            var result = await host.RegisterAsync(SelectedSim, PhoneLabel, scope);
            ApplyDevice(result.Device);
            await LoadStateAsync();
            Ui.Toast(Loc.Instance["sms_gateway_registered"]);
        });
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        if (!TryBuildScope(out var scope))
            return;
        await RunAsync(async () =>
        {
            var result = await host.UpdateConsentAsync(DeviceId, SimSlot, scope, false);
            if (result.RequiresConsent)
            {
                if (!await ConfirmScopeAsync(scope))
                    return;
                result = await host.UpdateConsentAsync(DeviceId, SimSlot, scope, true);
            }
            ApplyDevice(result.Device);
            Ui.Toast(Loc.Instance["success"]);
        });
    }

    [RelayCommand]
    private async Task TogglePauseAsync()
    {
        await RunAsync(async () =>
        {
            await host.SetPauseAsync(DeviceId, SimSlot, !IsPaused);
            await LoadStateAsync();
        });
    }

    [RelayCommand]
    private async Task SendTestAsync()
    {
        if (string.IsNullOrWhiteSpace(TestPhone))
        {
            Ui.Toast(Loc.Instance["sms_gateway_test_phone_required"]);
            return;
        }
        await RunAsync(async () =>
        {
            await host.SendTestAsync(SimSlot, TestPhone.Trim());
            await LoadStateAsync();
            Ui.Toast(Loc.Instance["sms_gateway_test_created"]);
        });
    }

    [RelayCommand]
    private async Task RevokeAsync()
    {
        if (!await Shell.Current.CurrentPage.DisplayAlertAsync(Loc.Instance["sms_gateway_revoke"],
                Loc.Instance["sms_gateway_revoke_confirm"], Loc.Instance["sms_gateway_revoke"], Loc.Instance["cancel"]))
            return;
        await RunAsync(async () =>
        {
            await host.SetConsentAsync(DeviceId, SimSlot, false);
            IsConsented = false;
            StatusReason = Loc.Instance["sms_gateway_status_consent_required"];
        });
    }

    private async Task LoadStateAsync()
    {
        var states = await host.GetStatesAsync();
        _states.Clear();
        Devices.Clear();
        foreach (var state in states)
        {
            _states[state.Device.SimSlot] = state;
            Devices.Add(new MobileSmsGatewayCard(state.Device));
        }
        if (SelectedSim is not null && _states.TryGetValue(SelectedSim.Slot, out var selected))
            ApplyState(selected);
        else if (SelectedSim is not null)
            ResetDevice(SelectedSim.Slot);
    }

    private void ApplyState(SmsGatewayHostStateDto state)
    {
        ApplyDevice(state.Device);
        TestMode = state.TestMode;
        MessageTypes = string.Join(" · ", EnabledTypes(state.MessageTypes));
        Jobs.Clear();
        foreach (var job in state.Jobs)
            Jobs.Add(job);
    }

    private void ApplyDevice(SmsGatewayDeviceDto device)
    {
        DeviceId = device.Id;
        SimSlot = device.SimSlot;
        IsRegistered = true;
        IsTrusted = device.IsTrusted;
        IsConsented = device.IsConsented;
        IsPaused = device.IsPaused;
        PhoneLabel = device.PhoneLabel ?? PhoneLabel;
        IsUnlimited = device.MonthlyQuota is null;
        MonthlyQuota = device.MonthlyQuota?.ToString() ?? string.Empty;
        QuotaResetDay = device.QuotaResetDay;
        MaxPerHour = device.MaxPerHour;
        MinIntervalSeconds = device.MinIntervalSeconds;
        LowQuotaWarnPercent = device.LowQuotaWarnPercent;
        StatusReason = Loc.Instance[$"sms_gateway_status_{device.BlockReason}"];
        Status = StatusReason;
        IsOverQuota = device.IsOverQuota;
        var days = Math.Max(0, (device.QuotaResetsAt.ToLocalTime().Date - DateTime.Today).Days);
        if (device.MonthlyQuota is not int limit)
        {
            ShowQuotaProgress = false;
            QuotaText = Loc.Instance["unlimited"];
            QuotaDetail = string.Format(Loc.Instance["sms_gateway_unlimited_usage"], device.SentThisPeriod, days);
            SetQuotaLevel(100);
            QuotaProgress = 0;
            return;
        }
        ShowQuotaProgress = true;
        QuotaText = $"{device.SentThisPeriod:N0} / {limit:N0}";
        QuotaDetail = IsOverQuota
            ? string.Format(Loc.Instance["sms_gateway_over_quota"], days)
            : IsQuotaExhausted
                ? Loc.Instance["sms_gateway_limit_exhausted"]
                : string.Format(Loc.Instance["sms_gateway_days_left"], days);
        QuotaProgress = limit <= 0 ? 1 : Math.Clamp((double)device.SentThisPeriod / limit, 0, 1);
        var remainingPercent = limit <= 0
            ? 0
            : (int)Math.Clamp(((long)limit - device.SentThisPeriod) * 100 / limit, 0, 100);
        SetQuotaLevel(remainingPercent);
        if (!IsOverQuota)
            QuotaDetail = IsQuotaExhausted
                ? Loc.Instance["sms_gateway_limit_exhausted"]
                : string.Format(Loc.Instance["sms_gateway_days_left"], days);
    }

    private void SetQuotaLevel(int remainingPercent)
    {
        IsQuotaExhausted = remainingPercent == 0;
        IsQuotaDanger = remainingPercent is > 0 and < 10;
        IsQuotaOrange = remainingPercent is >= 10 and < 25;
        IsQuotaWarning = remainingPercent is >= 25 and < 50;
        IsQuotaSuccess = remainingPercent >= 50;
    }

    private bool TryBuildScope(out SmsGatewayConsentScope scope)
    {
        scope = null!;
        int? quota = null;
        var value = 0;
        if (!IsUnlimited && (!int.TryParse(MonthlyQuota, out value) || value <= 0))
        {
            Ui.Toast(Loc.Instance["sms_gateway_limit_required"]);
            return false;
        }
        if (!IsUnlimited)
            quota = value;
        scope = new SmsGatewayConsentScope(quota, IsUnlimited, QuotaResetDay, MaxPerHour,
            MinIntervalSeconds, LowQuotaWarnPercent);
        return true;
    }

    private static async Task<bool> ConfirmScopeAsync(SmsGatewayConsentScope scope)
    {
        var limit = scope.IsUnlimited ? Loc.Instance["unlimited"] : scope.MonthlyQuota!.Value.ToString("N0");
        var message = string.Format(Loc.Instance["sms_gateway_consent_scope"], limit, scope.QuotaResetDay,
            scope.MinIntervalSeconds, scope.MaxPerHour);
        return await Shell.Current.CurrentPage.DisplayAlertAsync(Loc.Instance["sms_gateway_consent_title"], message,
            Loc.Instance["sms_gateway_consent_accept"], Loc.Instance["cancel"]);
    }

    private async Task RunAsync(Func<Task> action)
    {
        IsBusy = true;
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            Status = Describe(exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ReplaceSims(IReadOnlyList<MobileSimInfo> sims)
    {
        Sims.Clear();
        foreach (var sim in sims)
            Sims.Add(sim);
        var selectedSlot = Preferences.Get("sms_gateway_selected_slot", -1);
        SelectedSim = Sims.FirstOrDefault(x => x.Slot == selectedSlot) ?? Sims.FirstOrDefault();
    }

    private void ResetDevice(int simSlot)
    {
        SimSlot = simSlot;
        DeviceId = 0;
        IsRegistered = false;
        IsTrusted = false;
        IsConsented = false;
        IsPaused = false;
        StatusReason = Loc.Instance["sms_gateway_register_hint"];
        Status = StatusReason;
        Jobs.Clear();
    }

    private static IEnumerable<string> EnabledTypes(SmsGatewayMessageTypesDto types)
    {
        if (types.DebtReminder) yield return Loc.Instance["sms_gateway_debt"];
        if (types.ReceiptLink) yield return Loc.Instance["sms_gateway_receipt"];
        if (types.Manual) yield return Loc.Instance["sms_gateway_manual"];
        if (types.Promotion) yield return Loc.Instance["sms_gateway_promotion"];
    }

    private static string Describe(Exception exception) => exception is Refit.ApiException api
        ? ApiErrors.Describe(api)
        : exception.Message;
}

public partial class SmsGatewaySettingsViewModel(
    SmsGatewayHostService host,
    ISmsGatewayPlatform platform) : SmsGatewayViewModel(host, platform);

public sealed class MobileSmsGatewayCard
{
    public string Title { get; }
    public string Status { get; }
    public string Usage { get; }
    public string Remaining { get; }
    public double Progress { get; }
    public bool ShowProgress { get; }
    /// Rang qolgan ulushga qarab: yashil -> sariq -> to'q sariq -> qizil.
    public bool IsWarning { get; }
    public bool IsOrange { get; }
    public bool IsDanger { get; }
    public string Detail { get; }

    public MobileSmsGatewayCard(SmsGatewayDeviceDto device)
    {
        Title = $"SIM {device.SimSlot + 1} · {device.SimOperator}";
        Status = Loc.Instance[$"sms_gateway_status_{device.BlockReason}"];
        Usage = device.MonthlyQuota is null
            ? string.Format(Loc.Instance["sms_gateway_unlimited_usage"], device.SentThisPeriod, 0)
            : $"{device.SentThisPeriod:N0} / {device.MonthlyQuota:N0}";
        Remaining = device.MonthlyQuota is int limit
            ? $"{Math.Max(0, limit - device.SentThisPeriod):N0}"
            : Loc.Instance["unlimited"];
        ShowProgress = device.MonthlyQuota is not null;
        Progress = device.MonthlyQuota is > 0
            ? Math.Clamp((double)device.SentThisPeriod / device.MonthlyQuota.Value, 0, 1)
            : 0;
        var remainingPercent = device.MonthlyQuota is > 0
            ? Math.Clamp(100 - (int)Math.Round(Progress * 100), 0, 100)
            : 100;
        IsDanger = device.MonthlyQuota is > 0 && remainingPercent < 10;
        IsOrange = remainingPercent is >= 10 and < 25;
        IsWarning = remainingPercent is >= 25 and < 50;
        Detail = string.Format(Loc.Instance["sms_gateway_days_left"], DaysUntilReset(device.QuotaResetDay));
    }

    private static int DaysUntilReset(int resetDay)
    {
        var today = DateTime.Today;
        var day = Math.Clamp(resetDay, 1, 28);
        var next = new DateTime(today.Year, today.Month, day);
        if (next <= today) next = next.AddMonths(1);
        return (next - today).Days;
    }
}
