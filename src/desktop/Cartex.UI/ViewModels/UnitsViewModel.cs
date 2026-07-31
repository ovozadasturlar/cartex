using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Units;
using Cartex.UI.Models;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class UnitsViewModel : ViewModelBase, ILoadable
{
    private readonly IUnitsApi _api;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private readonly IExportService _export;
    private readonly AuthService _auth;
    private readonly List<UnitDto> _all = [];
    private long _editId;

    public ObservableCollection<UnitDto> Units { get; } = [];

    [ObservableProperty] private bool _isEditOpen;
    [ObservableProperty] private bool _isNew;
    [ObservableProperty] private string _editName = "";
    [ObservableProperty] private string _editShortName = "";
    [ObservableProperty] private string _editDimension = "Count";
    [ObservableProperty] private decimal _editFactor = 1;
    [ObservableProperty] private string? _searchText;

    public string[] DimensionOptions { get; } = ["Count", "Weight", "Volume", "Length"];

    public bool IsEmpty => Units.Count == 0;
    public bool CanExport => _auth.HasPermission("reports.export");
    public bool CanCreate => _auth.HasPermission("units.create");
    public bool CanEdit => _auth.HasPermission("units.edit");
    public bool CanToggle => _auth.HasPermission("units.toggle");

    private IReadOnlyList<PageShortcut>? _shortcuts;

    public IReadOnlyList<PageShortcut> Shortcuts => _shortcuts ??=
        CrudShortcuts(OpenCreateCommand, SaveCommand, () => IsEditOpen = false, () => IsEditOpen);

    public UnitsViewModel(IUnitsApi api, IToastService toast, IBusyService busy, IExportService export, AuthService auth)
    {
        _api = api;
        _toast = toast;
        _busy = busy;
        _export = export;
        _auth = auth;
    }

    partial void OnSearchTextChanged(string? value) => ApplyFilter();

    private void ApplyFilter()
    {
        var q = SearchText?.Trim();
        Units.Clear();
        foreach (var u in _all.Where(u => string.IsNullOrEmpty(q) || u.Name.Contains(q, StringComparison.OrdinalIgnoreCase)))
            Units.Add(u);
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    private async Task Export(string format)
    {
        try
        {
            var all = await _api.GetAllAsync();
            await _export.ExportAsync(L["units"], all,
            [
                new(L["name"], x => x.Name),
                new(L["short_name"], x => x.ShortName),
            ], Enum.Parse<ExportFormat>(format));
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    public async Task LoadAsync()
    {
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                var items = await _api.GetAllAsync();
                _all.Clear();
                _all.AddRange(items);
                ApplyFilter();
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
        EditShortName = "";
        EditDimension = "Count";
        EditFactor = 1;
        IsEditOpen = true;
    }

    [RelayCommand]
    private void OpenEdit(UnitDto unit)
    {
        if (!CanEdit) return;
        if (unit.IsSystem) return;
        IsNew = false;
        _editId = unit.Id;
        EditName = unit.Name;
        EditShortName = unit.ShortName;
        EditDimension = unit.Dimension;
        EditFactor = unit.Factor;
        IsEditOpen = true;
    }

    [RelayCommand]
    private async Task ToggleEnabled(UnitDto unit)
    {
        if (!CanToggle) return;
        try
        {
            await _api.SetStateAsync(unit.Id, new SetUnitStateRequest(!unit.IsEnabled, unit.IsDefault));
            await LoadAsync();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task MakeDefault(UnitDto unit)
    {
        if (unit.IsDefault) return;
        try
        {
            await _api.SetStateAsync(unit.Id, new SetUnitStateRequest(true, true));
            await LoadAsync();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private void CancelEdit() => IsEditOpen = false;

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsNew ? !CanCreate : !CanEdit) return;
        if (string.IsNullOrWhiteSpace(EditName) || string.IsNullOrWhiteSpace(EditShortName)) { _toast.Error(L["error"]); return; }
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                if (IsNew)
                    await _api.CreateAsync(new CreateUnitRequest(EditName.Trim(), EditShortName.Trim(), EditDimension, EditFactor));
                else
                    await _api.UpdateAsync(_editId, new UpdateUnitRequest(EditName.Trim(), EditShortName.Trim(), EditDimension, EditFactor));
            }
            IsEditOpen = false;
            ServiceLocator.Resolve<ReferenceCache>().Invalidate(CacheKeys.Units);
            _toast.Success(L["success"]);
            await LoadAsync();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }
}
