using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Loyalty;
using Cartex.UI.Models;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class ManufacturersViewModel : ViewModelBase, ILoadable
{
    private readonly IManufacturersApi _api;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private long _editId;

    public ObservableCollection<ManufacturerDto> Manufacturers { get; } = [];

    [ObservableProperty] private bool _isEditOpen;
    [ObservableProperty] private bool _isNew;
    [ObservableProperty] private string _editName = "";

    public bool IsEmpty => Manufacturers.Count == 0;

    private IReadOnlyList<PageShortcut>? _shortcuts;

    public IReadOnlyList<PageShortcut> Shortcuts => _shortcuts ??=
        CrudShortcuts(OpenCreateCommand, SaveCommand, () => IsEditOpen = false, () => IsEditOpen);

    public ManufacturersViewModel(IManufacturersApi api, IToastService toast, IBusyService busy)
    {
        _api = api;
        _toast = toast;
        _busy = busy;
    }

    public async Task LoadAsync()
    {
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                var items = await _api.GetAllAsync();
                Manufacturers.Clear();
                foreach (var m in items) Manufacturers.Add(m);
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private void OpenCreate()
    {
        IsNew = true;
        _editId = 0;
        EditName = "";
        IsEditOpen = true;
    }

    [RelayCommand]
    private void OpenEdit(ManufacturerDto manufacturer)
    {
        IsNew = false;
        _editId = manufacturer.Id;
        EditName = manufacturer.Name;
        IsEditOpen = true;
    }

    [RelayCommand]
    private void CancelEdit() => IsEditOpen = false;

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(EditName)) { _toast.Error(L["error"]); return; }
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                if (IsNew)
                    await _api.CreateAsync(new SaveManufacturerRequest(EditName.Trim()));
                else
                    await _api.UpdateAsync(_editId, new SaveManufacturerRequest(EditName.Trim()));
            }
            IsEditOpen = false;
            ServiceLocator.Resolve<ReferenceCache>().Invalidate(CacheKeys.Manufacturers);
            _toast.Success(L["success"]);
            await LoadAsync();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task DeleteAsync(ManufacturerDto manufacturer)
    {
        try
        {
            using (_busy.Begin(L["loading"]))
                await _api.DeleteAsync(manufacturer.Id);
            ServiceLocator.Resolve<ReferenceCache>().Invalidate(CacheKeys.Manufacturers);
            _toast.Success(L["success"]);
            await LoadAsync();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }
}
