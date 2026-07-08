using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Branches;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class BranchesViewModel : ViewModelBase, ILoadable
{
    private readonly IBranchesApi _api;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private long _editId;

    public ObservableCollection<BranchDto> Branches { get; } = [];

    [ObservableProperty] private bool _isEditOpen;
    [ObservableProperty] private bool _isNew;
    [ObservableProperty] private string _editName = "";
    [ObservableProperty] private string _editAddress = "";
    [ObservableProperty] private string _editPhone = "";
    [ObservableProperty] private bool _editIsActive = true;

    public bool IsEmpty => Branches.Count == 0;

    public BranchesViewModel(IBranchesApi api, IToastService toast, IBusyService busy)
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
                Branches.Clear();
                foreach (var b in items) Branches.Add(b);
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
        EditAddress = "";
        EditPhone = "";
        EditIsActive = true;
        IsEditOpen = true;
    }

    [RelayCommand]
    private void OpenEdit(BranchDto branch)
    {
        IsNew = false;
        _editId = branch.Id;
        EditName = branch.Name;
        EditAddress = branch.Address ?? "";
        EditPhone = branch.Phone ?? "";
        EditIsActive = branch.IsActive;
        IsEditOpen = true;
    }

    [RelayCommand]
    private void CancelEdit() => IsEditOpen = false;

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(EditName)) { _toast.Error(L["error"]); return; }
        var address = string.IsNullOrWhiteSpace(EditAddress) ? null : EditAddress.Trim();
        var phone = string.IsNullOrWhiteSpace(EditPhone) ? null : EditPhone.Trim();
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                if (IsNew)
                    await _api.CreateAsync(new CreateBranchRequest(EditName.Trim(), address, phone));
                else
                    await _api.UpdateAsync(_editId, new UpdateBranchRequest(EditName.Trim(), address, phone, EditIsActive));
            }
            IsEditOpen = false;
            _toast.Success(L["success"]);
            await LoadAsync();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }
}
