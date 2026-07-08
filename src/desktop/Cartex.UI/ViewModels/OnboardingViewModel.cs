using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Branches;
using Cartex.Shared.Models.Business;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class OnboardingViewModel(IBusinessApi businessApi, IBranchesApi branchesApi, IToastService toast, IBusyService busy) : ViewModelBase
{
    public Action? Completed { get; set; }

    public string[] Currencies { get; } = ["UZS", "USD", "RUB", "EUR", "KZT"];

    [ObservableProperty] private int _step = 1;
    [ObservableProperty] private string _businessName = string.Empty;
    [ObservableProperty] private string _currency = "UZS";
    [ObservableProperty] private string _branchName = string.Empty;
    [ObservableProperty] private string _branchAddress = string.Empty;

    public bool IsStep1 => Step == 1;
    public bool IsStep2 => Step == 2;
    public bool IsStep3 => Step == 3;
    public bool CanBack => Step > 1;
    public bool IsLast => Step == 3;

    partial void OnStepChanged(int value)
    {
        OnPropertyChanged(nameof(IsStep1));
        OnPropertyChanged(nameof(IsStep2));
        OnPropertyChanged(nameof(IsStep3));
        OnPropertyChanged(nameof(CanBack));
        OnPropertyChanged(nameof(IsLast));
    }

    public async Task LoadAsync()
    {
        try
        {
            var business = await businessApi.GetAsync();
            BusinessName = business.Name;
            Currency = string.IsNullOrEmpty(business.Currency) ? "UZS" : business.Currency;
        }
        catch { }
    }

    [RelayCommand]
    private void Next()
    {
        if (Step == 1 && string.IsNullOrWhiteSpace(BusinessName)) { toast.Warning(L["error"]); return; }
        if (Step < 3) Step++;
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
                await businessApi.CompleteOnboardingAsync();
            }
            toast.Success(L["success"]);
            Completed?.Invoke();
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
    }
}
