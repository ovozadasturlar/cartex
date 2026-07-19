using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Warehouses;
using Cartex.UI.Models;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class WarehousesViewModel : ViewModelBase, ILoadable
{
    private readonly IWarehousesApi _api;
    private readonly IBranchesApi _branchesApi;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private long _editId;

    public ObservableCollection<WarehouseDto> Warehouses { get; } = [];
    public ObservableCollection<IdOption> BranchOptions { get; } = [];

    [ObservableProperty] private bool _isEditOpen;
    [ObservableProperty] private bool _isNew;
    [ObservableProperty] private string _editName = "";
    [ObservableProperty] private bool _editIsOnline;
    [ObservableProperty] private IdOption? _selectedBranch;

    public bool IsEmpty => Warehouses.Count == 0;

    private IReadOnlyList<PageShortcut>? _shortcuts;

    public IReadOnlyList<PageShortcut> Shortcuts => _shortcuts ??=
        CrudShortcuts(OpenCreateCommand, SaveCommand, () => IsEditOpen = false, () => IsEditOpen);

    public WarehousesViewModel(IWarehousesApi api, IBranchesApi branchesApi, IToastService toast, IBusyService busy)
    {
        _api = api;
        _branchesApi = branchesApi;
        _toast = toast;
        _busy = busy;
    }

    public async Task LoadAsync()
    {
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                var branches = await _branchesApi.GetAllAsync();
                BranchOptions.Clear();
                foreach (var b in branches) BranchOptions.Add(new IdOption(b.Id, b.Name));

                var items = await _api.GetAllAsync();
                Warehouses.Clear();
                foreach (var w in items) Warehouses.Add(w);
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
        EditIsOnline = false;
        SelectedBranch = BranchOptions.FirstOrDefault();
        IsEditOpen = true;
    }

    [RelayCommand]
    private void OpenEdit(WarehouseDto warehouse)
    {
        IsNew = false;
        _editId = warehouse.Id;
        EditName = warehouse.Name;
        EditIsOnline = warehouse.IsOnline;
        SelectedBranch = BranchOptions.FirstOrDefault(b => b.Id == warehouse.BranchId);
        IsEditOpen = true;
    }

    [RelayCommand]
    private void CancelEdit() => IsEditOpen = false;

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(EditName) || SelectedBranch?.Id is null) { _toast.Error(L["error"]); return; }
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                if (IsNew)
                    await _api.CreateAsync(new CreateWarehouseRequest(SelectedBranch.Id.Value, EditName.Trim(), EditIsOnline));
                else
                    await _api.UpdateAsync(_editId, new UpdateWarehouseRequest(EditName.Trim(), EditIsOnline));
            }
            IsEditOpen = false;
            ServiceLocator.Resolve<ReferenceCache>().Invalidate(CacheKeys.Warehouses);
            _toast.Success(L["success"]);
            await LoadAsync();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }
}
