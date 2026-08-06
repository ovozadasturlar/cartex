using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Categories;
using Cartex.UI.Models;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class CategoriesViewModel : ViewModelBase, ILoadable
{
    private readonly ICategoriesApi _api;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private readonly IExportService _export;
    private readonly AuthService _auth;
    private readonly List<CategoryDto> _all = [];
    private long _editId;

    public ObservableCollection<CategoryDto> Categories { get; } = [];
    public ObservableCollection<IdOption> Parents { get; } = [];

    [ObservableProperty] private bool _isEditOpen;
    [ObservableProperty] private bool _isNew;
    [ObservableProperty] private string _editName = "";
    [ObservableProperty] private string _editDescription = "";
    [ObservableProperty] private IdOption? _selectedParent;
    [ObservableProperty] private string? _searchText;

    public bool IsEmpty => Categories.Count == 0;
    public bool CanExport => _auth.HasPermission("reports.export");
    public bool CanCreate => _auth.HasPermission("categories.create");
    public bool CanEdit => _auth.HasPermission("categories.edit");

    private IReadOnlyList<PageShortcut>? _shortcuts;

    public IReadOnlyList<PageShortcut> Shortcuts => _shortcuts ??=
        CrudShortcuts(OpenCreateCommand, SaveCommand, () => IsEditOpen = false, () => IsEditOpen);

    public CategoriesViewModel(ICategoriesApi api, IToastService toast, IBusyService busy, IExportService export, AuthService auth)
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
        Categories.Clear();
        foreach (var c in _all.Where(c => string.IsNullOrEmpty(q) || c.Name.Contains(q, StringComparison.OrdinalIgnoreCase)))
            Categories.Add(c);
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    private async Task Export(string format)
    {
        try
        {
            // Use the already-filtered Categories collection (respects SearchText local filter)
            var items = Categories.ToList();
            await _export.ExportAsync(L["categories"], items,
            [
                new(L["name"], x => x.Name),
                new(L["description"], x => x.Description),
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
                _all.AddRange(OrderHierarchically(items));
                Parents.Clear();
                Parents.Add(new IdOption(null, L["none"]));
                foreach (var c in items.OrderBy(x => x.Name))
                    Parents.Add(new IdOption(c.Id, c.Name));
                ApplyFilter();
            }
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    private static IEnumerable<CategoryDto> OrderHierarchically(IReadOnlyList<CategoryDto> items)
    {
        var byParent = items.ToLookup(c => c.ParentId);
        var result = new List<CategoryDto>();

        void AddChildren(long? parentId)
        {
            foreach (var c in byParent[parentId].OrderBy(c => c.Name)) { result.Add(c); AddChildren(c.Id); }
        }

        AddChildren(null);

        var included = result.Select(c => c.Id).ToHashSet();
        foreach (var c in items.Where(c => !included.Contains(c.Id)).OrderBy(c => c.Name))
            result.Add(c);

        return result;
    }

    [RelayCommand]
    private void OpenCreate()
    {
        if (!CanCreate) return;
        IsNew = true;
        _editId = 0;
        EditName = "";
        EditDescription = "";
        SelectedParent = Parents.FirstOrDefault();
        IsEditOpen = true;
    }

    [RelayCommand]
    private void OpenEdit(CategoryDto category)
    {
        if (!CanEdit) return;
        IsNew = false;
        _editId = category.Id;
        EditName = category.Name;
        EditDescription = category.Description ?? "";
        SelectedParent = Parents.FirstOrDefault(p => p.Id == category.ParentId) ?? Parents.FirstOrDefault();
        IsEditOpen = true;
    }

    [RelayCommand]
    private void CancelEdit() => IsEditOpen = false;

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsNew ? !CanCreate : !CanEdit) return;
        if (string.IsNullOrWhiteSpace(EditName)) { _toast.Error(L["error"]); return; }
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                var description = string.IsNullOrWhiteSpace(EditDescription) ? null : EditDescription.Trim();
                if (IsNew)
                    await _api.CreateAsync(new CreateCategoryRequest(EditName.Trim(), SelectedParent?.Id, description));
                else
                    await _api.UpdateAsync(_editId, new UpdateCategoryRequest(EditName.Trim(), SelectedParent?.Id, description));
            }
            IsEditOpen = false;
            ServiceLocator.Resolve<ReferenceCache>().Invalidate(CacheKeys.Categories);
            _toast.Success(L["success"]);
            await LoadAsync();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }
}
