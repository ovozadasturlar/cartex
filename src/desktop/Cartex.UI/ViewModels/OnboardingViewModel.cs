using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Branches;
using Cartex.Shared.Models.Business;
using Cartex.Shared.Models.Common;
using Cartex.UI.Models;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class PresetOption(string code, string title, string description) : ObservableObject
{
    public string Code { get; } = code;
    public string Title { get; } = title;
    public string Description { get; } = description;
    [ObservableProperty] private bool _isSelected;
}

public partial class OnboardingViewModel(IBusinessApi businessApi, IBranchesApi branchesApi, IToastService toast, IBusyService busy) : ViewModelBase
{
    public Action? Completed { get; set; }

    public string[] Currencies { get; } = CurrencyCatalog.All;

    public ObservableCollection<PresetOption> Presets { get; } = [];

    [ObservableProperty] private int _step = 1;
    [ObservableProperty] private string _businessName = string.Empty;
    [ObservableProperty] private string _currency = "UZS";
    [ObservableProperty] private PresetOption? _selectedPreset;
    [ObservableProperty] private string _branchName = string.Empty;
    [ObservableProperty] private string _branchAddress = string.Empty;

    public bool IsStep1 => Step == 1;
    public bool IsStep2 => Step == 2;
    public bool IsStep3 => Step == 3;
    public bool IsStep4 => Step == 4;
    public bool CanBack => Step > 1;
    public bool IsLast => Step == 4;

    partial void OnStepChanged(int value)
    {
        OnPropertyChanged(nameof(IsStep1));
        OnPropertyChanged(nameof(IsStep2));
        OnPropertyChanged(nameof(IsStep3));
        OnPropertyChanged(nameof(IsStep4));
        OnPropertyChanged(nameof(CanBack));
        OnPropertyChanged(nameof(IsLast));
    }

    public async Task LoadAsync()
    {
        Presets.Clear();
        foreach (var code in new[] { "minimarket", "construction", "clothing", "electronics", "pharmacy", "universal" })
            Presets.Add(new PresetOption(code, L[$"preset_{code}"], L[$"preset_{code}_hint"]));
        SelectPreset(Presets[^1]);

        try
        {
            var business = await businessApi.GetAsync();
            BusinessName = business.Name;
            Currency = string.IsNullOrEmpty(business.Currency) ? "UZS" : business.Currency;
        }
        catch { }
    }

    [RelayCommand]
    private void SelectPreset(PresetOption option)
    {
        foreach (var p in Presets) p.IsSelected = p == option;
        SelectedPreset = option;
    }

    [RelayCommand]
    private void Next()
    {
        if (Step == 1 && string.IsNullOrWhiteSpace(BusinessName)) { toast.Warning(L["error"]); return; }
        if (Step < 4) Step++;
    }

    [RelayCommand]
    private void Back()
    {
        if (Step > 1) Step--;
    }

    [RelayCommand]
    private async Task Finish()
    {
        if (string.IsNullOrWhiteSpace(BusinessName)) { Step = 1; toast.Warning(L["error"]); return; }
        try
        {
            using (busy.Begin(L["loading"]))
            {
                await businessApi.UpdateAsync(new UpdateBusinessRequest(BusinessName.Trim(), null, Currency));
                if (!string.IsNullOrWhiteSpace(BranchName))
                    await branchesApi.CreateAsync(new CreateBranchRequest(
                        BranchName.Trim(),
                        string.IsNullOrWhiteSpace(BranchAddress) ? null : BranchAddress.Trim(),
                        null));
                await businessApi.CompleteOnboardingAsync(new CompleteOnboardingRequest(SelectedPreset?.Code, SettingsService.Instance.Language switch
                {
                    AppLanguage.UzCyrl => "uz-cyrl",
                    AppLanguage.Ru => "ru",
                    AppLanguage.En => "en",
                    _ => "uz-latn"
                }));
            }
            toast.Success(L["success"]);
            Completed?.Invoke();
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
    }
}
