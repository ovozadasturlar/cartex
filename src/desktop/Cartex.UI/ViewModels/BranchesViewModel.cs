using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Branches;
using Cartex.UI.Models;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class BranchCatalogRow : ObservableObject
{
    private readonly Func<BranchCatalogRow, string, Task> _setVisibility;

    public long VariantId { get; }
    public string ProductName { get; }
    public string? Code { get; }
    public string? Barcode { get; }
    public bool IsActive { get; }
    [ObservableProperty] private string _visibilityOverride;
    [ObservableProperty] private bool _isUpdating;

    public BranchCatalogRow(BranchCatalogItemDto item, Func<BranchCatalogRow, string, Task> setVisibility)
    {
        VariantId = item.VariantId;
        ProductName = item.ProductName;
        Code = item.Code;
        Barcode = item.Barcode;
        IsActive = item.IsActive;
        _visibilityOverride = item.VisibilityOverride;
        _setVisibility = setVisibility;
    }

    [RelayCommand] private Task SetAuto() => _setVisibility(this, "Auto");
    [RelayCommand] private Task SetVisible() => _setVisibility(this, "ForceVisible");
    [RelayCommand] private Task SetHidden() => _setVisibility(this, "ForceHidden");
}

public partial class BranchesViewModel : ViewModelBase, ILoadable
{
    private readonly IBranchesApi _api;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private readonly AuthService _auth;
    private long _editId;

    public ObservableCollection<BranchDto> Branches { get; } = [];
    public ObservableCollection<BranchCatalogRow> CatalogItems { get; } = [];

    [ObservableProperty] private bool _isEditOpen;
    [ObservableProperty] private bool _isCatalogOpen;
    [ObservableProperty] private bool _isNew;
    [ObservableProperty] private string _editName = "";
    [ObservableProperty] private string _editAddress = "";
    [ObservableProperty] private string _editPhone = "";
    [ObservableProperty] private bool _editIsActive = true;
    [ObservableProperty] private string _catalogBranchName = string.Empty;
    [ObservableProperty] private string _catalogSearch = string.Empty;
    [ObservableProperty] private int _catalogTotalCount;
    private long _catalogBranchId;
    private int _catalogGeneration;
    private CancellationTokenSource? _catalogSearchCts;

    public bool IsEmpty => Branches.Count == 0;
    public bool IsModalOpen => IsEditOpen || IsCatalogOpen;
    public bool CanCreate => _auth.HasPermission("branches.create");
    public bool CanEdit => _auth.HasPermission("branches.edit");
    public bool CanEditCatalog => _auth.HasPermission("products.edit");

    private IReadOnlyList<PageShortcut>? _shortcuts;

    public IReadOnlyList<PageShortcut> Shortcuts => _shortcuts ??=
        CrudShortcuts(OpenCreateCommand, SaveCommand, () => IsEditOpen = false, () => IsEditOpen);

    public BranchesViewModel(IBranchesApi api, IToastService toast, IBusyService busy, AuthService auth)
    {
        _api = api;
        _toast = toast;
        _busy = busy;
        _auth = auth;
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
        if (!CanCreate) return;
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
        if (!CanEdit) return;
        IsNew = false;
        _editId = branch.Id;
        EditName = branch.Name;
        EditAddress = branch.Address ?? "";
        EditPhone = branch.Phone ?? "";
        EditIsActive = branch.IsActive;
        IsEditOpen = true;
    }

    [RelayCommand]
    private async Task OpenCatalogAsync(BranchDto branch)
    {
        _catalogBranchId = branch.Id;
        CatalogBranchName = branch.Name;
        CatalogSearch = string.Empty;
        IsCatalogOpen = true;
        await LoadCatalogAsync();
    }

    [RelayCommand]
    private void CancelEdit() => IsEditOpen = false;

    [RelayCommand]
    private void CloseCatalog()
    {
        _catalogSearchCts?.Cancel();
        IsCatalogOpen = false;
    }

    partial void OnIsEditOpenChanged(bool value) => OnPropertyChanged(nameof(IsModalOpen));
    partial void OnIsCatalogOpenChanged(bool value) => OnPropertyChanged(nameof(IsModalOpen));

    partial void OnCatalogSearchChanged(string value)
    {
        if (!IsCatalogOpen)
            return;

        _catalogSearchCts?.Cancel();
        var cts = _catalogSearchCts = new CancellationTokenSource();
        _ = DebouncedCatalogSearchAsync(cts.Token);
    }

    private async Task DebouncedCatalogSearchAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(250, cancellationToken);
            if (!cancellationToken.IsCancellationRequested)
                await LoadCatalogAsync();
        }
        catch (OperationCanceledException) { }
    }

    private async Task LoadCatalogAsync()
    {
        if (_catalogBranchId == 0)
            return;

        var generation = ++_catalogGeneration;
        try
        {
            var page = await _api.GetCatalogAsync(_catalogBranchId, string.IsNullOrWhiteSpace(CatalogSearch) ? null : CatalogSearch.Trim());
            if (generation != _catalogGeneration || !IsCatalogOpen)
                return;

            CatalogItems.Clear();
            foreach (var item in page.Items)
                CatalogItems.Add(new BranchCatalogRow(item, SetCatalogVisibilityAsync));
            CatalogTotalCount = page.TotalCount;
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    private async Task SetCatalogVisibilityAsync(BranchCatalogRow row, string visibility)
    {
        if (!CanEditCatalog) return;
        if (row.IsUpdating || row.VisibilityOverride == visibility)
            return;

        row.IsUpdating = true;
        try
        {
            await _api.SetCatalogVisibilityAsync(_catalogBranchId, row.VariantId, new SetBranchCatalogVisibilityRequest(visibility));
            row.VisibilityOverride = visibility;
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
        finally { row.IsUpdating = false; }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsNew ? !CanCreate : !CanEdit) return;
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
