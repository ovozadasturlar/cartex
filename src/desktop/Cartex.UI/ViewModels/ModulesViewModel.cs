using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Features;
using Cartex.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.UI.ViewModels;

/// The owner's side of the two-layer switch (SOZ-08). The vendor screen under the developer
/// section decides what the shop bought; this one decides what the shop uses.
public partial class ModulesViewModel(IFeaturesApi api, IToastService toast, IBusyService busy, AuthService auth)
    : ViewModelBase, ILoadable
{
    public ObservableCollection<OwnerModuleDto> Modules { get; } = [];

    [ObservableProperty] private bool _isLoading;

    public bool CanEdit => auth.HasPermission("business.edit");

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var modules = await api.GetModulesAsync();
            Modules.Clear();
            foreach (var module in modules) Modules.Add(module);
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task ToggleAsync(OwnerModuleDto module)
    {
        if (!CanEdit || module is null || !module.Available) return;
        try
        {
            using (busy.Begin(L["loading"]))
                await api.SetModuleAsync(module.Code, new SetFeatureRequest(!module.IsEnabled));

            // Kassa yoqilgan modullar ro'yxatini keshdan o'qiydi. Keshni bo'shatmasak,
            // o'chirilgan modulning tugmasi yana 10 daqiqa ko'rinib turadi.
            ServiceLocator.Resolve<ReferenceCache>().Invalidate(CacheKeys.Features);
            await LoadAsync();
            toast.Success(L["success"]);
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
    }
}
