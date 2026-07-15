using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Roles;
using Cartex.Shared.Models.Permissions;
using Cartex.UI.Models;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class RolesViewModel : ViewModelBase, ILoadable
{
    private readonly IRolesApi _rolesApi;
    private readonly IPermissionsApi _permissionsApi;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private readonly AuthService _auth;
    private readonly IExportService _export;

    private List<PermissionDto> _allPermissions = [];
    private List<RoleDto> _all = [];
    private long _editId;

    private readonly Dictionary<string, PermissionItem> _permIndex = [];
    private Dictionary<string, string[]> _deps = [];

    public ObservableCollection<RoleDto> Roles { get; } = [];
    public ObservableCollection<StartPageOption> StartPages { get; } = [];
    public ObservableCollection<StartPageOption> CartDestinations { get; } = [];
    public ObservableCollection<PermissionGroup> PermissionGroups { get; } = [];
    public ObservableCollection<PermissionGroup> GrantablePermissionGroups { get; } = [];
    public ObservableCollection<PermissionItem> AssignableRoleItems { get; } = [];

    [ObservableProperty] private bool _hasRolesManageSelected;
    [ObservableProperty] private bool _hasUsersManageSelected;

    public bool ShowGrantableTab => CanGovern && HasRolesManageSelected;

    partial void OnHasRolesManageSelectedChanged(bool value) => OnPropertyChanged(nameof(ShowGrantableTab));

    private void RefreshDynamicTabs()
    {
        HasRolesManageSelected = _permIndex.TryGetValue("roles.manage", out var rm) && rm.IsSelected;
        HasUsersManageSelected = _permIndex.TryGetValue("users.manage", out var um) && um.IsSelected;
    }

    private void BuildAssignableRoles(IReadOnlyCollection<string> selected)
    {
        AssignableRoleItems.Clear();
        foreach (var role in _all.Where(r => !r.AccessAll))
        {
            var item = new PermissionItem { Id = role.Id, Name = role.Name, Label = role.Name };
            item.Configure(selected.Contains(role.Name), null);
            AssignableRoleItems.Add(item);
        }
    }

    public bool CanGovern => _auth.HasPermission("permissions.govern");

    [ObservableProperty] private bool _isEditOpen;
    [ObservableProperty] private bool _isNew;
    [ObservableProperty] private string _editName = "";
    [ObservableProperty] private string _editDescription = "";
    [ObservableProperty] private int _editPriority;
    [ObservableProperty] private StartPageOption? _selectedStartPage;
    [ObservableProperty] private StartPageOption? _selectedCartDestination;
    [ObservableProperty] private string? _searchText;

    public bool IsEmpty => Roles.Count == 0;
    public bool CanExport => _auth.HasPermission("reports.export");
    public string EditTitle => IsNew ? L["create_role"] : L["edit_role"];

    private IReadOnlyList<PageShortcut>? _shortcuts;
    public IReadOnlyList<PageShortcut> Shortcuts => _shortcuts ??= CrudShortcuts(OpenCreateCommand, SaveCommand, () => IsEditOpen = false, () => IsEditOpen);

    public RolesViewModel(IRolesApi rolesApi, IPermissionsApi permissionsApi, IToastService toast, IBusyService busy, AuthService auth, IExportService export)
    {
        _rolesApi = rolesApi;
        _permissionsApi = permissionsApi;
        _toast = toast;
        _busy = busy;
        _auth = auth;
        _export = export;
    }

    public async Task LoadAsync()
    {
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                _allPermissions = await _permissionsApi.GetAllAsync();
                _deps = _allPermissions.ToDictionary(p => p.Name, p => p.DependsOn.ToArray());
                _all = (await _rolesApi.GetAllAsync()).ToList();
                ApplyFilter();
                BuildStartPages();
                BuildCartDestinations();
            }
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    partial void OnSearchTextChanged(string? value) => ApplyFilter();

    private void ApplyFilter()
    {
        var q = SearchText?.Trim();
        IEnumerable<RoleDto> roles = _all;
        if (!string.IsNullOrEmpty(q))
            roles = roles.Where(r => r.Name.Contains(q, StringComparison.OrdinalIgnoreCase));
        Roles.Clear();
        foreach (var r in roles) Roles.Add(r);
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    private async Task Export(string format)
    {
        try
        {
            var all = await _rolesApi.GetAllAsync();
            await _export.ExportAsync(L["roles"], all,
            [
                new(L["name"], r => r.Name),
                new(L["description"], r => r.Description),
            ], Enum.Parse<ExportFormat>(format));
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    private void BuildStartPages()
    {
        StartPages.Clear();
        StartPages.Add(new StartPageOption(null, L["none"]));
        foreach (var p in NavRegistry.SidebarPages)
            StartPages.Add(new StartPageOption(p.Key, L[p.Key]));
    }

    private void BuildCartDestinations()
    {
        CartDestinations.Clear();
        CartDestinations.Add(new StartPageOption(null, "—"));
        CartDestinations.Add(new StartPageOption("queue", L["dest_queue"]));
        CartDestinations.Add(new StartPageOption("order", L["dest_order"]));
    }

    private void BuildPermissionGroups(ObservableCollection<PermissionGroup> target, IReadOnlyCollection<string> selected, bool cascade)
    {
        target.Clear();
        if (cascade) _permIndex.Clear();
        foreach (var group in _allPermissions.Where(p => p.IsEnabled).GroupBy(p => p.Name.Split('.')[0]).OrderBy(g => g.Key))
        {
            var g = new PermissionGroup { Title = LocalizationManager.Instance.Find($"perm_group_{group.Key}") ?? group.Key };
            foreach (var perm in group)
            {
                var item = new PermissionItem { Id = perm.Id, Name = perm.Name, Label = LocalizationManager.Instance.Find($"perm_{perm.Name}") ?? perm.Description ?? perm.Name };
                item.Configure(selected.Contains(perm.Name), cascade ? OnPermissionToggled : null);
                g.Items.Add(item);
                if (cascade) _permIndex[perm.Name] = item;
            }
            target.Add(g);
        }
    }

    private void OnPermissionToggled(PermissionItem item)
    {
        if (item.IsSelected)
        {
            foreach (var required in RequiredNames(item.Name))
                if (_permIndex.TryGetValue(required, out var dep) && !dep.IsSelected) dep.SetSelected(true);
        }
        else
        {
            foreach (var dependent in DependentNames(item.Name))
                if (_permIndex.TryGetValue(dependent, out var it) && it.IsSelected) it.SetSelected(false);
        }
        RefreshDynamicTabs();
    }

    private HashSet<string> RequiredNames(string name)
    {
        var result = new HashSet<string>();
        var stack = new Stack<string>(_deps.TryGetValue(name, out var d) ? d : []);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (result.Add(current) && _deps.TryGetValue(current, out var next))
                foreach (var n in next) stack.Push(n);
        }
        return result;
    }

    private IEnumerable<string> DependentNames(string name) =>
        _deps.Keys.Where(p => RequiredNames(p).Contains(name));

    private void NormalizeDependencies()
    {
        foreach (var item in _permIndex.Values.Where(i => i.IsSelected).ToList())
            foreach (var required in RequiredNames(item.Name))
                if (_permIndex.TryGetValue(required, out var dep)) dep.SetSelected(true);
    }

    [RelayCommand]
    private void OpenCreate()
    {
        IsNew = true;
        _editId = 0;
        EditName = "";
        EditDescription = "";
        EditPriority = 0;
        SelectedStartPage = StartPages.FirstOrDefault();
        SelectedCartDestination = CartDestinations.FirstOrDefault();
        BuildPermissionGroups(PermissionGroups, [], true);
        BuildPermissionGroups(GrantablePermissionGroups, [], false);
        BuildAssignableRoles([]);
        RefreshDynamicTabs();
        OnPropertyChanged(nameof(EditTitle));
        IsEditOpen = true;
    }

    [RelayCommand]
    private void OpenEdit(RoleDto role)
    {
        IsNew = false;
        _editId = role.Id;
        EditName = role.Name;
        EditDescription = role.Description ?? "";
        EditPriority = role.Priority;
        SelectedStartPage = StartPages.FirstOrDefault(s => s.Key == role.StartPage) ?? StartPages.FirstOrDefault();
        SelectedCartDestination = CartDestinations.FirstOrDefault(s => s.Key == role.CartDestination) ?? CartDestinations.FirstOrDefault();
        BuildPermissionGroups(PermissionGroups, role.Permissions, true);
        NormalizeDependencies();
        BuildPermissionGroups(GrantablePermissionGroups, role.GrantablePermissions, false);
        BuildAssignableRoles(role.AssignableRoles);
        RefreshDynamicTabs();
        OnPropertyChanged(nameof(EditTitle));
        IsEditOpen = true;
    }

    [RelayCommand]
    private void CancelEdit() => IsEditOpen = false;

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(EditName)) { _toast.Error(L["error"]); return; }

        var permissionIds = PermissionGroups.SelectMany(g => g.Items).Where(i => i.IsSelected).Select(i => i.Id).ToList();
        var startPage = SelectedStartPage?.Key;
        var cartDestination = SelectedCartDestination?.Key;
        var description = string.IsNullOrWhiteSpace(EditDescription) ? null : EditDescription.Trim();
        var grantable = ShowGrantableTab
            ? GrantablePermissionGroups.SelectMany(g => g.Items).Where(i => i.IsSelected).Select(i => i.Name).ToList()
            : null;
        var assignableRoles = HasUsersManageSelected
            ? AssignableRoleItems.Where(i => i.IsSelected).Select(i => i.Name).ToList()
            : null;

        try
        {
            using (_busy.Begin(L["loading"]))
            {
                long id;
                if (IsNew)
                    id = await _rolesApi.CreateAsync(new CreateRoleRequest(EditName.Trim(), description, startPage, EditPriority, grantable, assignableRoles, CartDestination: cartDestination));
                else
                {
                    id = _editId;
                    await _rolesApi.UpdateAsync(id, new UpdateRoleRequest(EditName.Trim(), description, startPage, EditPriority, grantable, assignableRoles, CartDestination: cartDestination));
                }
                await _rolesApi.AssignPermissionsAsync(id, new AssignPermissionsRequest(permissionIds));
            }
            IsEditOpen = false;
            _toast.Success(L["success"]);
            await LoadAsync();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }
}

public partial class PermissionItem : ObservableObject
{
    public long Id { get; init; }
    public string Name { get; init; } = "";
    public string Label { get; init; } = "";

    private Action<PermissionItem>? _onToggled;
    private bool _suppress;

    [ObservableProperty] private bool _isSelected;

    public void Configure(bool selected, Action<PermissionItem>? onToggled)
    {
        _suppress = true;
        IsSelected = selected;
        _suppress = false;
        _onToggled = onToggled;
    }

    public void SetSelected(bool value)
    {
        _suppress = true;
        IsSelected = value;
        _suppress = false;
    }

    partial void OnIsSelectedChanged(bool value)
    {
        if (!_suppress) _onToggled?.Invoke(this);
    }
}

public class PermissionGroup
{
    public string Title { get; init; } = "";
    public ObservableCollection<PermissionItem> Items { get; } = [];
}
