using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Settings;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class ReceiptSettingsViewModel(ISettingsApi api, IToastService toast, IBusyService busy) : ViewModelBase, ILoadable
{
    public int[] PaperWidths { get; } = [32, 42, 48];

    [ObservableProperty] private string _headerText = string.Empty;
    [ObservableProperty] private string _footerText = string.Empty;
    [ObservableProperty] private int _paperWidth = 32;

    public async Task LoadAsync()
    {
        try
        {
            var cfg = await api.GetReceiptAsync();
            HeaderText = cfg.HeaderText ?? string.Empty;
            FooterText = cfg.FooterText ?? string.Empty;
            PaperWidth = cfg.PaperWidth;
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            using (busy.Begin(L["loading"]))
                await api.UpdateReceiptAsync(new UpdateReceiptSettingsRequest(
                    string.IsNullOrWhiteSpace(HeaderText) ? null : HeaderText.Trim(),
                    string.IsNullOrWhiteSpace(FooterText) ? null : FooterText.Trim(),
                    PaperWidth));
            toast.Success(L["success"]);
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
    }
}
