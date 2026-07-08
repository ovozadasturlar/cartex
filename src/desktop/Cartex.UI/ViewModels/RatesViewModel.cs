using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Common;
using Cartex.Shared.Models.Rates;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class RatesViewModel(IRatesApi api, IBusinessApi businessApi, IToastService toast, IBusyService busy) : ViewModelBase, ILoadable
{
    public ObservableCollection<RateDto> Rates { get; } = [];
    public ObservableCollection<RateDto> History { get; } = [];
    public ObservableCollection<string> Codes { get; } = new(CurrencyCatalog.All);

    [ObservableProperty] private string _baseCurrency = "";
    [ObservableProperty] private bool _isFeatureOff;
    [ObservableProperty] private string? _newCode = "USD";
    [ObservableProperty] private decimal _newRate;
    [ObservableProperty] private RateDto? _selectedRate;

    public bool IsEmpty => Rates.Count == 0;

    public async Task LoadAsync()
    {
        try
        {
            using (busy.Begin(L["loading"]))
            {
                var business = await businessApi.GetAsync();
                BaseCurrency = business.Currency;
                IsFeatureOff = !business.Multicurrency;
                Codes.Remove(BaseCurrency);

                var rates = await api.GetCurrentAsync();
                Rates.Clear();
                foreach (var r in rates.OrderBy(r => r.Code)) Rates.Add(r);
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
    }

    partial void OnSelectedRateChanged(RateDto? value)
    {
        if (value is not null) _ = LoadHistoryAsync(value.Code);
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

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(NewCode) || NewRate <= 0) { toast.Error(L["error"]); return; }
        try
        {
            using (busy.Begin(L["loading"]))
                await api.SetAsync(new SetRateRequest(NewCode.Trim().ToUpperInvariant(), NewRate));
            toast.Success(L["success"]);
            await LoadAsync();
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
    }
}
