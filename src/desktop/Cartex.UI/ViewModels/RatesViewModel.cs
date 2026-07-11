using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Rates;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class CurrencyRow(CurrencyDto dto, bool isStale) : ObservableObject
{
    public string Code { get; } = dto.Code;
    public string Name { get; } = dto.Name;
    public bool IsSystem { get; } = dto.IsSystem;
    public bool IsBase { get; } = dto.IsBase;
    public bool IsCustom { get; } = !dto.IsSystem && !dto.IsBase;
    public bool IsEnabled { get; } = dto.IsEnabled;
    public bool IsDefault { get; } = dto.IsDefault;
    public decimal? Rate { get; } = dto.Rate;
    public DateTime? RateAt { get; } = dto.RateAt;
    public bool HasRate { get; } = dto.Rate is not null;
    public bool IsStale { get; } = isStale;
    [ObservableProperty] private decimal _newRate;
}

public partial class RatesViewModel(IRatesApi api, IBusinessApi businessApi, ISettingsApi settingsApi, AuthService auth, IDialogService dialog, IToastService toast, IBusyService busy) : ViewModelBase, ILoadable
{
    public ObservableCollection<CurrencyRow> Currencies { get; } = [];
    public ObservableCollection<RateDto> History { get; } = [];

    [ObservableProperty] private string _baseCurrency = "";
    [ObservableProperty] private bool _isFeatureOff;
    [ObservableProperty] private int _activeCount;
    [ObservableProperty] private int _staleCount;
    [ObservableProperty] private bool _isAddOpen;
    [ObservableProperty] private string _newCode = "";
    [ObservableProperty] private string _newName = "";
    [ObservableProperty] private string? _historyCode;

    public bool CanManageCurrencies => auth.HasPermission("currencies.manage");
    public bool CanManageRates => auth.HasPermission("rates.manage");
    public bool HasStale => StaleCount > 0;

    partial void OnStaleCountChanged(int value) => OnPropertyChanged(nameof(HasStale));

    private int _staleDays = 3;

    public async Task LoadAsync()
    {
        try
        {
            using (busy.Begin(L["loading"]))
            {
                var business = await businessApi.GetAsync();
                BaseCurrency = business.Currency;
                IsFeatureOff = !business.Multicurrency;
                try { _staleDays = (await ServiceLocator.Resolve<ReferenceCache>().GetAsync(CacheKeys.SalesPolicy, settingsApi.GetSalesPolicyAsync)).StaleRateDays; } catch { }

                var list = await api.GetCurrenciesAsync();
                var limit = DateTime.UtcNow.AddDays(-_staleDays);
                Currencies.Clear();
                foreach (var c in list.OrderByDescending(c => c.IsBase).ThenBy(c => c.Code))
                    Currencies.Add(new CurrencyRow(c, c.IsEnabled && !c.IsBase && (c.RateAt is null || c.RateAt < limit)));
                ActiveCount = list.Count(c => c.IsEnabled);
                StaleCount = Currencies.Count(c => c.IsStale);
                if (HistoryCode is { } code && Currencies.All(c => c.Code != code))
                {
                    HistoryCode = null;
                    History.Clear();
                }
            }
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task SaveRateAsync(CurrencyRow row)
    {
        if (row.NewRate <= 0) { toast.Error(L["error"]); return; }
        try
        {
            using (busy.Begin(L["loading"]))
                await api.SetAsync(new SetRateRequest(row.Code, row.NewRate));
            ServiceLocator.Resolve<ReferenceCache>().Invalidate(CacheKeys.Rates);
            toast.Success(L["success"]);
            await LoadAsync();
            if (HistoryCode == row.Code) await LoadHistoryAsync(row.Code);
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task ToggleEnabledAsync(CurrencyRow row)
    {
        if (row.IsBase) return;
        try
        {
            await api.UpdateCurrencyAsync(row.Code, new UpdateCurrencyRequest(row.Code, !row.IsEnabled, row.IsDefault));
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
        await LoadAsync();
    }

    [RelayCommand]
    private async Task MakeDefaultAsync(CurrencyRow row)
    {
        if (row.IsDefault) return;
        try
        {
            await api.UpdateCurrencyAsync(row.Code, new UpdateCurrencyRequest(row.Code, row.IsEnabled, true));
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
        await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteAsync(CurrencyRow row)
    {
        if (!await dialog.ConfirmDangerAsync(string.Format(L["currency_delete_confirm"], row.Code), L["delete"])) return;
        try
        {
            await api.DeleteCurrencyAsync(row.Code);
            toast.Success(L["success"]);
            await LoadAsync();
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private void OpenAdd()
    {
        NewCode = "";
        NewName = "";
        IsAddOpen = true;
    }

    [RelayCommand]
    private void CancelAdd() => IsAddOpen = false;

    [RelayCommand]
    private async Task AddAsync()
    {
        var code = NewCode.Trim().ToUpperInvariant();
        if (code.Length < 2 || string.IsNullOrWhiteSpace(NewName)) { toast.Warning(L["required_fields_hint"]); return; }
        try
        {
            using (busy.Begin(L["loading"]))
                await api.CreateCurrencyAsync(new CreateCurrencyRequest(code, NewName.Trim()));
            IsAddOpen = false;
            toast.Success(L["success"]);
            await LoadAsync();
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private Task ShowHistoryAsync(CurrencyRow row)
    {
        HistoryCode = row.Code;
        return LoadHistoryAsync(row.Code);
    }

    private async Task LoadHistoryAsync(string code)
    {
        try
        {
            var history = await api.GetHistoryAsync(code);
            History.Clear();
            foreach (var r in history) History.Add(r);
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
    }
}
