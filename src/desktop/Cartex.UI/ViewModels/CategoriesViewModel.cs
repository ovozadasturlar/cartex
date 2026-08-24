using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Categories;
using Cartex.Shared.Search;
using Cartex.UI.Models;
using Cartex.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.UI.ViewModels;

public partial class CategoriesViewModel : ViewModelBase, ILoadable
{
    private readonly ICategoriesApi _api;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private readonly IExportService _export;
    private readonly AuthService _auth;
    private readonly List<CategoryDto> _all = [];
    private readonly Dictionary<long, CategoryTreeItem> _nodes = [];
    private readonly HashSet<long> _expanded = [];
    private long _editId;
    private CategoryTreeItem? _mergeSource;

    public ObservableCollection<CategoryTreeItem> Categories { get; } = [];
    public ObservableCollection<IdOption> Parents { get; } = [];
    public ObservableCollection<IdOption> MergeTargets { get; } = [];

    [ObservableProperty] private bool _isEditOpen;
    [ObservableProperty] private bool _isMergeOpen;
    [ObservableProperty] private bool _isNew;
    [ObservableProperty] private string _editName = "";
    [ObservableProperty] private string _editDescription = "";
    [ObservableProperty] private IdOption? _selectedParent;
    [ObservableProperty] private IdOption? _selectedMergeTarget;
    [ObservableProperty] private string? _searchText;

    public bool IsEmpty => Categories.Count == 0;
    public bool IsOverlayOpen => IsEditOpen || IsMergeOpen;
    public bool CanExport => _auth.HasPermission("reports.export");
    public bool CanCreate => _auth.HasPermission("categories.create");
    public bool CanEdit => _auth.HasPermission("categories.edit");
    public string ExpandCollapseText => _nodes.Values.Where(x => x.HasChildren).All(x => _expanded.Contains(x.Id))
        ? L["collapse_all"]
        : L["expand_all"];
    public string MergeConfirmation => string.Format(
        CultureInfo.CurrentCulture,
        L["category_merge_confirm"],
        _mergeSource?.ProductCount ?? 0,
        _mergeSource?.FullPath ?? "",
        SelectedMergeTarget?.Name ?? "");

    private IReadOnlyList<PageShortcut>? _shortcuts;

    public IReadOnlyList<PageShortcut> Shortcuts => _shortcuts ??=
        CrudShortcuts(OpenCreateCommand, SaveCommand, CloseOverlay, () => IsOverlayOpen);

    public CategoriesViewModel(ICategoriesApi api, IToastService toast, IBusyService busy, IExportService export, AuthService auth)
    {
        _api = api;
        _toast = toast;
        _busy = busy;
        _export = export;
        _auth = auth;
        _expanded.UnionWith(SettingsService.Instance.GetExpandedCategoryIds(auth.UserInfo?.UserId ?? 0));
    }

    partial void OnSearchTextChanged(string? value) => ApplyFilter();
    partial void OnIsEditOpenChanged(bool value) => OnPropertyChanged(nameof(IsOverlayOpen));
    partial void OnIsMergeOpenChanged(bool value) => OnPropertyChanged(nameof(IsOverlayOpen));
    partial void OnSelectedMergeTargetChanged(IdOption? value) => OnPropertyChanged(nameof(MergeConfirmation));

    private void ApplyFilter()
    {
        var query = SearchFold.Fuzzy(SearchText);
        var isSearching = query.Length > 0;
        var included = new HashSet<long>();
        var automaticExpanded = new HashSet<long>();

        foreach (var node in _nodes.Values)
        {
            node.IsMatch = isSearching && SearchFold.Fuzzy(node.FullPath).Contains(query, StringComparison.Ordinal);
            if (!node.IsMatch) continue;
            included.Add(node.Id);
            var parentId = node.ParentId;
            while (parentId is { } id && _nodes.TryGetValue(id, out var parent))
            {
                included.Add(parent.Id);
                automaticExpanded.Add(parent.Id);
                parentId = parent.ParentId;
            }
        }

        Categories.Clear();
        void Add(long? parentId)
        {
            foreach (var node in Siblings(parentId))
            {
                if (isSearching && !included.Contains(node.Id)) continue;
                node.IsExpanded = _expanded.Contains(node.Id) || automaticExpanded.Contains(node.Id);
                Categories.Add(node);
                if (node.IsExpanded) Add(node.Id);
            }
        }
        Add(null);
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(ExpandCollapseText));
    }

    [RelayCommand]
    private void ToggleExpand(CategoryTreeItem item)
    {
        if (!item.HasChildren) return;
        item.IsExpanded = !item.IsExpanded;
        if (item.IsExpanded) _expanded.Add(item.Id);
        else _expanded.Remove(item.Id);
        SaveExpanded();
        ApplyFilter();
    }

    [RelayCommand]
    private void ExpandCollapseAll()
    {
        var expandable = _nodes.Values.Where(x => x.HasChildren).ToList();
        var expand = expandable.Any(x => !_expanded.Contains(x.Id));
        _expanded.Clear();
        if (expand) _expanded.UnionWith(expandable.Select(x => x.Id));
        SaveExpanded();
        ApplyFilter();
    }

    [RelayCommand]
    private async Task Export(string format)
    {
        try
        {
            var items = Categories.Select(x => x.Category).ToList();
            await _export.ExportAsync(L["categories"], items,
            [
                new(L["name"], x => x.FullPath ?? x.Name),
                new(L["products_count"], x => x.DescendantProductCount),
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
                _all.AddRange(items);
                _nodes.Clear();
                foreach (var category in items)
                    _nodes[category.Id] = new CategoryTreeItem(category);
                foreach (var node in _nodes.Values)
                {
                    node.Children = _nodes.Values.Where(x => x.ParentId == node.Id).ToList();
                    node.HasChildren = node.Children.Count > 0;
                }
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
        EditDescription = "";
        FillParents(null);
        SelectedParent = Parents.FirstOrDefault();
        IsEditOpen = true;
    }

    [RelayCommand]
    private void OpenEdit(CategoryTreeItem item)
    {
        if (!CanEdit) return;
        IsNew = false;
        _editId = item.Id;
        EditName = item.Name;
        EditDescription = item.Category.Description ?? "";
        FillParents(item);
        SelectedParent = Parents.FirstOrDefault(x => x.Id == item.ParentId) ?? Parents.FirstOrDefault();
        IsEditOpen = true;
    }

    [RelayCommand]
    private void OpenMerge(CategoryTreeItem item)
    {
        if (!CanEdit) return;
        _mergeSource = item;
        MergeTargets.Clear();
        foreach (var target in _nodes.Values.Where(x => CanMerge(item, x)).OrderBy(x => x.FullPath))
            MergeTargets.Add(new IdOption(target.Id, target.FullPath));
        SelectedMergeTarget = MergeTargets.FirstOrDefault();
        IsMergeOpen = true;
        OnPropertyChanged(nameof(MergeConfirmation));
    }

    [RelayCommand]
    private void CancelEdit() => CloseOverlay();

    private void CloseOverlay()
    {
        IsEditOpen = false;
        IsMergeOpen = false;
    }

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
            CloseOverlay();
            await ChangedAsync();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task MergeAsync()
    {
        if (!CanEdit || _mergeSource is null || SelectedMergeTarget?.Id is not { } targetId) return;
        try
        {
            using (_busy.Begin(L["loading"]))
                await _api.MergeAsync(_mergeSource.Id, new MergeCategoryRequest(targetId));
            _expanded.Remove(_mergeSource.Id);
            CloseOverlay();
            await ChangedAsync();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    public bool PreviewDrop(long sourceId, long targetId, double relativeY)
    {
        ClearDrop();
        if (!_nodes.TryGetValue(sourceId, out var source) || !_nodes.TryGetValue(targetId, out var target))
            return false;
        var position = relativeY < 0.25
            ? CategoryDropPosition.Before
            : relativeY > 0.75
                ? CategoryDropPosition.After
                : CategoryDropPosition.Child;
        var valid = TryPlan(source, target, position, out _);
        target.DropPosition = valid ? position : CategoryDropPosition.Invalid;
        return valid;
    }

    public async Task DropAsync(long sourceId, long targetId, double relativeY)
    {
        ClearDrop();
        if (!_nodes.TryGetValue(sourceId, out var source) || !_nodes.TryGetValue(targetId, out var target)) return;
        var position = relativeY < 0.25
            ? CategoryDropPosition.Before
            : relativeY > 0.75
                ? CategoryDropPosition.After
                : CategoryDropPosition.Child;
        if (!TryPlan(source, target, position, out var plan)) return;
        await MoveAsync(source, plan);
    }

    public void ClearDrop()
    {
        foreach (var node in _nodes.Values)
            node.DropPosition = CategoryDropPosition.None;
    }

    public async Task MoveByKeyboardAsync(CategoryTreeItem source, CategoryKeyboardMove move)
    {
        if (!CanEdit) return;
        CategoryMovePlan? plan = move switch
        {
            CategoryKeyboardMove.Up => OrderPlan(source, -1),
            CategoryKeyboardMove.Down => OrderPlan(source, 1),
            CategoryKeyboardMove.Outdent => OutdentPlan(source),
            CategoryKeyboardMove.Indent => IndentPlan(source),
            _ => null
        };
        if (plan is { } validPlan && IsValid(source, validPlan))
            await MoveAsync(source, validPlan);
    }

    private async Task MoveAsync(CategoryTreeItem source, CategoryMovePlan plan)
    {
        try
        {
            using (_busy.Begin(L["loading"]))
                await _api.MoveAsync(source.Id, new MoveCategoryRequest(plan.ParentId, plan.SortOrder));
            _expanded.Add(source.Id);
            await ChangedAsync();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    private bool TryPlan(CategoryTreeItem source, CategoryTreeItem target, CategoryDropPosition position, out CategoryMovePlan plan)
    {
        if (source.Id == target.Id || !CanEdit)
        {
            plan = default;
            return false;
        }
        if (position == CategoryDropPosition.Child)
        {
            plan = new CategoryMovePlan(target.Id, Siblings(target.Id).Count);
            return IsValid(source, plan);
        }
        var siblings = Siblings(target.ParentId).Where(x => x.Id != source.Id).ToList();
        var index = siblings.FindIndex(x => x.Id == target.Id);
        plan = new CategoryMovePlan(target.ParentId, index + (position == CategoryDropPosition.After ? 1 : 0));
        return index >= 0 && IsValid(source, plan);
    }

    private bool IsValid(CategoryTreeItem source, CategoryMovePlan plan)
    {
        if (plan.ParentId == source.Id) return false;
        if (plan.ParentId is { } parentId && IsDescendant(source.Id, parentId)) return false;
        var newDepth = plan.ParentId is { } id && _nodes.TryGetValue(id, out var parent) ? parent.Depth + 1 : 1;
        return newDepth + Height(source.Id) - 1 <= 3;
    }

    private CategoryMovePlan? OrderPlan(CategoryTreeItem source, int direction)
    {
        var siblings = Siblings(source.ParentId);
        var index = siblings.FindIndex(x => x.Id == source.Id);
        var target = index + direction;
        return index >= 0 && target >= 0 && target < siblings.Count
            ? new CategoryMovePlan(source.ParentId, target)
            : null;
    }

    private CategoryMovePlan? OutdentPlan(CategoryTreeItem source)
    {
        if (source.ParentId is not { } parentId || !_nodes.TryGetValue(parentId, out var parent)) return null;
        var siblings = Siblings(parent.ParentId).Where(x => x.Id != source.Id).ToList();
        var parentIndex = siblings.FindIndex(x => x.Id == parent.Id);
        return parentIndex < 0 ? null : new CategoryMovePlan(parent.ParentId, parentIndex + 1);
    }

    private CategoryMovePlan? IndentPlan(CategoryTreeItem source)
    {
        var siblings = Siblings(source.ParentId);
        var index = siblings.FindIndex(x => x.Id == source.Id);
        if (index <= 0) return null;
        var parent = siblings[index - 1];
        return new CategoryMovePlan(parent.Id, Siblings(parent.Id).Count);
    }

    private bool CanMerge(CategoryTreeItem source, CategoryTreeItem target)
    {
        if (source.Id == target.Id || IsDescendant(source.Id, target.Id)) return false;
        return source.Children.All(child => target.Depth + Height(child.Id) <= 3);
    }

    private bool IsDescendant(long sourceId, long candidateId)
    {
        var cursor = (long?)candidateId;
        while (cursor is { } id && _nodes.TryGetValue(id, out var node))
        {
            if (id == sourceId) return true;
            cursor = node.ParentId;
        }
        return false;
    }

    private int Height(long id)
    {
        var children = Siblings(id);
        return children.Count == 0 ? 1 : 1 + children.Max(x => Height(x.Id));
    }

    private List<CategoryTreeItem> Siblings(long? parentId) => _nodes.Values
        .Where(x => x.ParentId == parentId)
        .OrderBy(x => x.SortOrder)
        .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
        .ThenBy(x => x.Id)
        .ToList();

    private void FillParents(CategoryTreeItem? source)
    {
        Parents.Clear();
        Parents.Add(new IdOption(null, L["none"]));
        foreach (var node in _nodes.Values.OrderBy(x => x.FullPath))
        {
            if (source is null ? node.Depth < 3 : node.Id != source.Id && !IsDescendant(source.Id, node.Id)
                && node.Depth + Height(source.Id) <= 3)
                Parents.Add(new IdOption(node.Id, node.FullPath));
        }
    }

    private async Task ChangedAsync()
    {
        ServiceLocator.Resolve<ReferenceCache>().Invalidate(CacheKeys.Categories);
        _toast.Success(L["success"]);
        SaveExpanded();
        await LoadAsync();
    }

    private void SaveExpanded() => SettingsService.Instance.SetExpandedCategoryIds(_auth.UserInfo?.UserId ?? 0, _expanded);

    private readonly record struct CategoryMovePlan(long? ParentId, int SortOrder);
}

public sealed partial class CategoryTreeItem(CategoryDto category) : ObservableObject
{
    public CategoryDto Category { get; } = category;
    public long Id => Category.Id;
    public string Name => Category.Name;
    public string FullPath => Category.FullPath ?? Category.Name;
    public long? ParentId => Category.ParentId;
    public int SortOrder => Category.SortOrder;
    public int Depth => Category.Depth;
    public int ProductCount => Category.ProductCount;
    public int DescendantProductCount => Category.DescendantProductCount;
    public double ChevronAngle => IsExpanded ? 90 : 0;
    public Thickness Indent => new(Math.Max(0, Depth - 1) * 24, 0, 0, 0);
    public IReadOnlyCollection<CategoryTreeItem> Children { get; internal set; } = [];

    [ObservableProperty] private bool _hasChildren;
    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private bool _isMatch;
    [ObservableProperty] private CategoryDropPosition _dropPosition;

    public bool IsDropBefore => DropPosition == CategoryDropPosition.Before;
    public bool IsDropAfter => DropPosition == CategoryDropPosition.After;
    public bool IsDropChild => DropPosition == CategoryDropPosition.Child;
    public bool IsDropInvalid => DropPosition == CategoryDropPosition.Invalid;

    partial void OnDropPositionChanged(CategoryDropPosition value)
    {
        OnPropertyChanged(nameof(IsDropBefore));
        OnPropertyChanged(nameof(IsDropAfter));
        OnPropertyChanged(nameof(IsDropChild));
        OnPropertyChanged(nameof(IsDropInvalid));
    }

    partial void OnIsExpandedChanged(bool value) => OnPropertyChanged(nameof(ChevronAngle));
}

public enum CategoryDropPosition
{
    None,
    Before,
    After,
    Child,
    Invalid
}

public enum CategoryKeyboardMove
{
    Up,
    Down,
    Outdent,
    Indent
}
