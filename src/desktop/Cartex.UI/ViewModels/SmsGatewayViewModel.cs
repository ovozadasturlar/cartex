using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Settings;
using Cartex.Shared.Models.SmsGateway;
using Cartex.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.UI.ViewModels;

public partial class SmsGatewayViewModel(
    ISmsGatewayApi gatewayApi,
    ISettingsApi settingsApi,
    BranchContextService branch,
    IToastService toast,
    IBusyService busy,
    SettingsHubViewModel settingsHub) : ViewModelBase, ILoadable
{
    private readonly SmsAllowedPhoneNumbers _allowedNumbers = new();
    private SmsSettingsDto? _loaded;
    private string _aggregatorProvider = "eskiz";
    private string? _baseUrl;

    public ObservableCollection<SmsGatewayDeviceRow> Devices { get; } = [];
    public ObservableCollection<string> AllowedNumbers => _allowedNumbers.Items;
    public IReadOnlyList<string> TransportOptions { get; } =
    [
        LocalizationManager.Instance["sms_transport_phone"],
        LocalizationManager.Instance["sms_transport_aggregator"],
        LocalizationManager.Instance["sms_transport_fallback"]
    ];

    [ObservableProperty] private bool _enabled;
    [ObservableProperty] private int _transportMode;
    [ObservableProperty] private string _login = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _sender = string.Empty;
    [ObservableProperty] private bool _testMode = true;
    [ObservableProperty] private string _newAllowedNumber = string.Empty;
    [ObservableProperty] private int _fallbackAfterMinutes = 30;
    [ObservableProperty] private int _debtReminderStickyWaitMinutes = 15;
    [ObservableProperty] private int _receiptLinkStickyWaitMinutes;
    [ObservableProperty] private int _promotionStickyWaitMinutes = 60;
    [ObservableProperty] private int _manualStickyWaitMinutes;

    public bool ShowAggregatorFields => TransportMode > 0;
    public bool ShowFallbackDelay => TransportMode == 2;
    public bool HasMultipleDevices => ShouldShowStickyWaits(Devices.Count);

    partial void OnTransportModeChanged(int value)
    {
        OnPropertyChanged(nameof(ShowAggregatorFields));
        OnPropertyChanged(nameof(ShowFallbackDelay));
    }

    public async Task LoadAsync()
    {
        if (branch.CurrentBranchId is not long branchId)
            return;
        try
        {
            using (busy.Begin(L["loading"]))
            {
                var settingsTask = settingsApi.GetAsync();
                var devicesTask = gatewayApi.GetDevicesAsync(branchId);
                await Task.WhenAll(settingsTask, devicesTask);
                Apply(settingsTask.Result.Sms);
                Devices.Clear();
                foreach (var device in devicesTask.Result)
                    Devices.Add(new SmsGatewayDeviceRow(device));
                OnPropertyChanged(nameof(HasMultipleDevices));
            }
        }
        catch (Exception exception)
        {
            toast.Error(ApiErrors.Describe(exception));
        }
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    [RelayCommand]
    private void AddAllowedNumber()
    {
        if (_allowedNumbers.Add(NewAllowedNumber))
            NewAllowedNumber = string.Empty;
    }

    [RelayCommand]
    private void RemoveAllowedNumber(string number) => _allowedNumbers.Remove(number);

    [RelayCommand]
    private void OpenNotificationJournal() => settingsHub.SelectNotificationJournal();

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        if (_loaded is null)
            return;
        var provider = TransportMode == 1 ? _aggregatorProvider : "device";
        var fallback = TransportMode == 2 ? _aggregatorProvider : "none";
        var usesAggregator = TransportMode > 0;
        try
        {
            using (busy.Begin(L["loading"]))
                await settingsApi.UpdateSmsAsync(new UpdateSmsSettingsRequest(
                    Enabled,
                    provider,
                    usesAggregator ? Empty(Login) : null,
                    usesAggregator ? Empty(Password) : null,
                    usesAggregator ? Empty(Sender) : null,
                    usesAggregator ? _baseUrl : null,
                    fallback,
                    ShowFallbackDelay ? FallbackAfterMinutes : 0,
                    _loaded.DebtReminderEnabled,
                    _loaded.ReceiptLinkEnabled,
                    _loaded.PromotionEnabled,
                    _loaded.ManualEnabled,
                    _loaded.DebtReminderTemplate,
                    _loaded.ReceiptLinkTemplate,
                    _loaded.PromotionTemplate,
                    _loaded.ManualTemplate,
                    _loaded.SendReceiptOnSale,
                    TestMode,
                    AllowedNumbers,
                    DebtReminderStickyWaitMinutes,
                    ReceiptLinkStickyWaitMinutes,
                    PromotionStickyWaitMinutes,
                    ManualStickyWaitMinutes,
                    _loaded.QuietHoursEnabled,
                    _loaded.SendWindowStart,
                    _loaded.SendWindowEnd));
            toast.Success(L["success"]);
        }
        catch (Exception exception)
        {
            toast.Error(ApiErrors.Describe(exception));
        }
    }

    [RelayCommand]
    private async Task SaveDeviceAsync(SmsGatewayDeviceRow row)
    {
        try
        {
            await gatewayApi.UpdateAsync(row.Id, new UpdateSmsGatewayDeviceRequest(row.IsEnabled, row.Priority));
            toast.Success(L["success"]);
            await LoadAsync();
        }
        catch (Exception exception)
        {
            toast.Error(ApiErrors.Describe(exception));
        }
    }

    [RelayCommand]
    private async Task ToggleTrustAsync(SmsGatewayDeviceRow row)
    {
        try
        {
            await gatewayApi.SetTrustAsync(row.Id, new SetSmsGatewayTrustRequest(!row.IsTrusted));
            await LoadAsync();
        }
        catch (Exception exception)
        {
            toast.Error(ApiErrors.Describe(exception));
        }
    }

    private void Apply(SmsSettingsDto settings)
    {
        _loaded = settings;
        Enabled = settings.Enabled;
        _aggregatorProvider = settings.Provider != "device"
            ? settings.Provider
            : settings.FallbackProvider != "none" ? settings.FallbackProvider : "eskiz";
        TransportMode = settings.Provider != "device" ? 1 : settings.FallbackProvider != "none" ? 2 : 0;
        Login = settings.Login ?? string.Empty;
        Password = string.Empty;
        Sender = settings.Sender ?? string.Empty;
        _baseUrl = settings.BaseUrl;
        FallbackAfterMinutes = settings.FallbackAfterMinutes;
        TestMode = settings.TestMode;
        _allowedNumbers.Replace(settings.TestAllowedNumbers ?? []);
        DebtReminderStickyWaitMinutes = settings.DebtReminderStickyWaitMinutes;
        ReceiptLinkStickyWaitMinutes = settings.ReceiptLinkStickyWaitMinutes;
        PromotionStickyWaitMinutes = settings.PromotionStickyWaitMinutes;
        ManualStickyWaitMinutes = settings.ManualStickyWaitMinutes;
    }

    public static bool ShouldShowStickyWaits(int deviceCount) => deviceCount > 1;

    private static string? Empty(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public partial class SmsGatewayDeviceRow : ObservableObject
{
    public long Id { get; }
    public string Name { get; }
    public string Sim { get; }
    public string State { get; }
    public string Consent { get; }
    public string Usage { get; }
    public string StatusReason { get; }
    public string QuotaDetail { get; }
    public double QuotaProgress { get; }
    public bool HasQuota { get; }
    public bool IsQuotaSuccess => QuotaLevel == SmsQuotaLevel.Success;
    public bool IsQuotaWarning => QuotaLevel == SmsQuotaLevel.Warning;
    public bool IsQuotaOrange => QuotaLevel == SmsQuotaLevel.Orange;
    public bool IsQuotaDanger => QuotaLevel is SmsQuotaLevel.Danger or SmsQuotaLevel.Exhausted;
    public SmsQuotaLevel QuotaLevel { get; }
    public string? LastError { get; }
    public string LastSent { get; }
    public string LinkedCustomers { get; }
    public string Remaining { get; }

    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] private bool _isTrusted;
    [ObservableProperty] private int _priority;

    public SmsGatewayDeviceRow(SmsGatewayDeviceDto device)
    {
        Id = device.Id;
        Name = device.DeviceName;
        Sim = $"SIM {device.SimSlot + 1} · {device.SimOperator}";
        State = LocalizationManager.Instance[device.IsOnline ? "online" : "offline"];
        Consent = LocalizationManager.Instance[device.IsConsented ? "consent_granted" : "consent_notasked"];
        Usage = device.MonthlyQuota is null ? $"{device.SentThisPeriod:N0} / ∞" : $"{device.SentThisPeriod:N0} / {device.MonthlyQuota:N0}";
        StatusReason = LocalizationManager.Instance[$"sms_gateway_status_{device.BlockReason}"];
        var days = Math.Max(0, (device.QuotaResetsAt.ToLocalTime().Date - DateTime.Today).Days);
        QuotaDetail = device.IsOverQuota
            ? string.Format(LocalizationManager.Instance["sms_gateway_over_quota"], days)
            : string.Format(LocalizationManager.Instance["sms_gateway_days_left"], days);
        HasQuota = device.MonthlyQuota is not null;
        QuotaProgress = device.MonthlyQuota is > 0
            ? Math.Clamp((double)device.SentThisPeriod / device.MonthlyQuota.Value * 100, 0, 100)
            : 100;
        QuotaLevel = QuotaLevelFor(device.SentThisPeriod, device.MonthlyQuota);
        LastError = device.LastError;
        LastSent = device.LastSentAt?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? "—";
        LinkedCustomers = device.LinkedCustomerCount.ToString("N0");
        Remaining = device.MonthlyQuota is int limit
            ? Math.Max(0, limit - device.SentThisPeriod).ToString("N0")
            : "∞";
        IsEnabled = device.IsEnabled;
        IsTrusted = device.IsTrusted;
        Priority = device.Priority;
    }

    public static SmsQuotaLevel QuotaLevelFor(int used, int? limit)
    {
        if (limit is null)
            return SmsQuotaLevel.Success;
        var remaining = limit <= 0 ? 0 : Math.Clamp(((long)limit.Value - used) * 100 / limit.Value, 0, 100);
        return remaining switch
        {
            >= 50 => SmsQuotaLevel.Success,
            >= 25 => SmsQuotaLevel.Warning,
            >= 10 => SmsQuotaLevel.Orange,
            > 0 => SmsQuotaLevel.Danger,
            _ => SmsQuotaLevel.Exhausted
        };
    }
}

public enum SmsQuotaLevel
{
    Success,
    Warning,
    Orange,
    Danger,
    Exhausted
}
