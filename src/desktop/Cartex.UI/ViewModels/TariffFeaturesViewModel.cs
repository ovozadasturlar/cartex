using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Features;
using Cartex.Shared.Models.Licensing;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class TariffFeatureVm : ObservableObject
{
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public string Impact { get; init; } = "";
    [ObservableProperty] private bool _isEnabled;
}

public partial class TariffFeaturesViewModel(
    ILicenseApi licenseApi,
    IFeaturesApi featuresApi,
    IToastService toast,
    IBusyService busy,
    AuthService auth)
    : ViewModelBase, ILoadable
{
    private Dictionary<string, List<string>> _includedTariffs = [];
    private bool _suppress;

    public ObservableCollection<string> Tariffs { get; } = [];
    public ObservableCollection<TariffFeatureVm> Features { get; } = [];

    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private string? _tariff;
    [ObservableProperty] private DateTimeOffset? _expiresAt;
    public bool CanEdit => auth.HasPermission("features.edit");

    public async Task LoadAsync()
    {
        try
        {
            using (busy.Begin(L["loading"]))
            {
                var options = await licenseApi.GetOptionsAsync();
                var status = await licenseApi.GetAsync();
                // Litsenziya ro'yxati va features jadvali ikkalasi ham sotuvchi darvozasi —
                // sahifa ikkisining kesishmasini (amaldagi holatni) ko'rsatadi, aks holda
                // bazada o'chiq modul yoniq bo'lib ko'rinadi.
                var vendorOff = (await featuresApi.GetAllAsync())
                    .Where(f => !f.IsEnabled).Select(f => f.Code).ToHashSet();

                _includedTariffs = options.Features.ToDictionary(f => f.Code, f => f.IncludedTariffs);
                var enabled = status.EnabledFeatures.Count > 0
                    ? status.EnabledFeatures
                    : options.Features.Where(f => f.IncludedTariffs.Contains(status.Tariff)).Select(f => f.Code).ToList();

                _suppress = true;
                Tariffs.Clear();
                foreach (var t in options.Tariffs) Tariffs.Add(t);
                Features.Clear();
                foreach (var f in options.Features)
                    Features.Add(new TariffFeatureVm
                    {
                        Code = f.Code,
                        Name = f.Name,
                        Impact = f.Permissions.Count > 0 ? string.Join(", ", f.Permissions) : L[$"feature_effect_{f.Code}"],
                        IsEnabled = enabled.Contains(f.Code) && !vendorOff.Contains(f.Code)
                    });
                IsActive = status.IsActive;
                Tariff = options.Tariffs.Contains(status.Tariff) ? status.Tariff : options.Tariffs.FirstOrDefault();
                ExpiresAt = status.ExpiresAt is { } e ? new DateTimeOffset(e) : null;
                _suppress = false;
            }
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
    }

    partial void OnTariffChanged(string? value)
    {
        if (_suppress || value is null) return;
        foreach (var f in Features)
            f.IsEnabled = _includedTariffs.TryGetValue(f.Code, out var tariffs) && tariffs.Contains(value);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!CanEdit) return;
        try
        {
            using (busy.Begin(L["loading"]))
            {
                var codes = Features.Where(f => f.IsEnabled).Select(f => f.Code).ToList();
                await licenseApi.UpdateAsync(new UpdateLicenseRequest(Tariff ?? "", ExpiresAt?.UtcDateTime, JsonSerializer.Serialize(codes)));
                foreach (var f in Features)
                    await featuresApi.SetAsync(f.Code, new SetFeatureRequest(f.IsEnabled));
            }
            ServiceLocator.Resolve<ReferenceCache>().Invalidate(CacheKeys.Features, CacheKeys.Business, CacheKeys.Rates, CacheKeys.SalesPolicy);
            await ServiceLocator.Resolve<NavigationService>().RequestFeaturesRefreshAsync();
            toast.Success(L["success"]);
            await LoadAsync();
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
    }
}
