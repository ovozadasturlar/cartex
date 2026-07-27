using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Permissions;
using Cartex.Shared.Models.Roles;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public record RoleColumn(long Id, string Name);

public partial class MatrixCell : ObservableObject
{
    public long RoleId { get; }
    public long PermissionId { get; }
    private readonly Func<MatrixCell, Task> _onToggled;
    private bool _suppress;

    [ObservableProperty] private bool _isAssigned;
    [ObservableProperty] private bool _isInteractive;

    public MatrixCell(long roleId, long permissionId, bool assigned, bool interactive, Func<MatrixCell, Task> onToggled)
    {
        RoleId = roleId;
        PermissionId = permissionId;
        _onToggled = onToggled;
        _suppress = true;
        IsAssigned = assigned;
        _suppress = false;
        IsInteractive = interactive;
    }

    public void Revert()
    {
        _suppress = true;
        IsAssigned = !IsAssigned;
        _suppress = false;
    }

    public void SetAssigned(bool value)
    {
        _suppress = true;
        IsAssigned = value;
        _suppress = false;
    }

    [RelayCommand]
    private Task Toggle() => IsInteractive && !_suppress ? _onToggled(this) : Task.CompletedTask;
}

public partial class MatrixRow : ObservableObject
{
    private readonly IReadOnlyList<RoleColumn> _roles;
    private readonly Func<long, long, bool> _isAssigned;
    private readonly Func<MatrixCell, Task> _onCellToggled;
    private readonly Action<MatrixCell> _registerCell;
    private ObservableCollection<MatrixCell>? _cells;
    public long PermissionId { get; init; }
    public string Name { get; init; } = "";
    public string? Description { get; init; }
    public string? GroupTitle { get; init; }
    public bool HasGroupTitle => !string.IsNullOrWhiteSpace(GroupTitle);
    public bool IsHeader { get; init; }
    public bool IsPermission => !IsHeader;
    public bool CanGovern { get; init; }
    public ObservableCollection<MatrixCell> Cells => _cells ??= CreateCells();

    private readonly Func<MatrixRow, Task> _onGlobalToggled;
    private bool _suppress;

    [ObservableProperty] private bool _isGloballyEnabled;

    public MatrixRow(
        IReadOnlyList<RoleColumn> roles,
        Func<long, long, bool> isAssigned,
        Func<MatrixCell, Task> onCellToggled,
        Action<MatrixCell> registerCell,
        Func<MatrixRow, Task> onGlobalToggled)
    {
        _roles = roles;
        _isAssigned = isAssigned;
        _onCellToggled = onCellToggled;
        _registerCell = registerCell;
        _onGlobalToggled = onGlobalToggled;
    }

    private ObservableCollection<MatrixCell> CreateCells()
    {
        var cells = new ObservableCollection<MatrixCell>();
        if (IsHeader) return cells;

        foreach (var role in _roles)
        {
            var cell = new MatrixCell(role.Id, PermissionId, _isAssigned(role.Id, PermissionId), IsGloballyEnabled, _onCellToggled);
            cells.Add(cell);
            _registerCell(cell);
        }

        return cells;
    }

    public void SetEnabled(bool value)
    {
        _suppress = true;
        IsGloballyEnabled = value;
        _suppress = false;
    }

    public void RevertGlobal()
    {
        _suppress = true;
        IsGloballyEnabled = !IsGloballyEnabled;
        _suppress = false;
    }

    [RelayCommand]
    private Task ToggleGlobal() => _suppress ? Task.CompletedTask : _onGlobalToggled(this);
}

public partial class PermissionsMatrixViewModel : ViewModelBase, ILoadable
{
    private readonly IRolesApi _rolesApi;
    private readonly IPermissionsApi _permissionsApi;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private readonly AuthService _auth;

    private readonly Dictionary<long, HashSet<long>> _rolePermissionIds = [];
    private readonly Dictionary<long, long[]> _dependencies = [];
    private readonly Dictionary<long, HashSet<long>> _dependents = [];
    private readonly Dictionary<(long RoleId, long PermissionId), MatrixCell> _cells = [];

    public ObservableCollection<RoleColumn> Roles { get; } = [];
    public ObservableCollection<MatrixRow> Rows { get; } = [];

    public bool CanGovern => _auth.HasPermission("permissions.govern");
    public bool IsEmpty => Rows.Count <= 1 || Roles.Count == 0;

    public PermissionsMatrixViewModel(IRolesApi rolesApi, IPermissionsApi permissionsApi, IToastService toast, IBusyService busy, AuthService auth)
    {
        _rolesApi = rolesApi;
        _permissionsApi = permissionsApi;
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
                var permissions = await _permissionsApi.GetAllAsync();
                var roles = (await _rolesApi.GetAllAsync()).Where(r => !r.AccessAll).ToList();
                var nameToId = permissions.ToDictionary(p => p.Name, p => p.Id);

                _dependencies.Clear();
                _dependents.Clear();
                _cells.Clear();
                foreach (var permission in permissions)
                {
                    var dependencies = permission.DependsOn.Where(nameToId.ContainsKey).Select(name => nameToId[name]).ToArray();
                    _dependencies[permission.Id] = dependencies;
                    foreach (var dependency in dependencies)
                    {
                        if (!_dependents.TryGetValue(dependency, out var dependents))
                            _dependents[dependency] = dependents = [];
                        dependents.Add(permission.Id);
                    }
                }

                _rolePermissionIds.Clear();
                foreach (var role in roles)
                    _rolePermissionIds[role.Id] = role.Permissions
                        .Where(nameToId.ContainsKey).Select(n => nameToId[n]).ToHashSet();

                Roles.Clear();
                foreach (var role in roles) Roles.Add(new RoleColumn(role.Id, role.Name));

                Rows.Clear();
                Rows.Add(new MatrixRow(Roles, IsAssigned, ToggleCellAsync, RegisterCell, ToggleGlobalAsync) { IsHeader = true });
                foreach (var group in permissions.OrderBy(p => p.Name).GroupBy(p => p.Name.Split('.')[0]).OrderBy(g => g.Key))
                {
                    var groupTitle = LocalizationManager.Instance.Find($"perm_group_{group.Key}") ?? group.Key;
                    var isFirst = true;
                    foreach (var perm in group)
                    {
                        var row = new MatrixRow(Roles, IsAssigned, ToggleCellAsync, RegisterCell, ToggleGlobalAsync)
                        {
                            PermissionId = perm.Id,
                            Name = LocalizationManager.Instance.Find($"perm_{perm.Name}") ?? perm.Description ?? perm.Name,
                            Description = perm.Name,
                            CanGovern = CanGovern,
                            GroupTitle = isFirst ? groupTitle : null
                        };
                        row.SetEnabled(perm.IsEnabled);
                        Rows.Add(row);
                        isFirst = false;
                    }
                }

                OnPropertyChanged(nameof(IsEmpty));
            }
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    private bool IsAssigned(long roleId, long permissionId) =>
        _rolePermissionIds.TryGetValue(roleId, out var ids) && ids.Contains(permissionId);

    private void RegisterCell(MatrixCell cell) => _cells[(cell.RoleId, cell.PermissionId)] = cell;

    private async Task ToggleCellAsync(MatrixCell cell)
    {
        var set = _rolePermissionIds[cell.RoleId];
        var ids = cell.IsAssigned
            ? RequiredIds(cell.PermissionId).Append(cell.PermissionId)
            : DependentIds(cell.PermissionId).Append(cell.PermissionId);

        foreach (var permissionId in ids)
        {
            if (cell.IsAssigned) set.Add(permissionId); else set.Remove(permissionId);
            if (_cells.TryGetValue((cell.RoleId, permissionId), out var related))
                related.SetAssigned(cell.IsAssigned);
        }
        try
        {
            await _rolesApi.AssignPermissionsAsync(cell.RoleId, new AssignPermissionsRequest(set.ToList()));
            _toast.Success(L["success"]);
        }
        catch
        {
            await LoadAsync();
            _toast.Error(L["error"]);
        }
    }

    private IReadOnlySet<long> RequiredIds(long permissionId)
    {
        var result = new HashSet<long>();
        var stack = new Stack<long>(_dependencies.TryGetValue(permissionId, out var ids) ? ids : []);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!result.Add(current) || !_dependencies.TryGetValue(current, out var next))
                continue;
            foreach (var item in next)
                stack.Push(item);
        }
        return result;
    }

    private IReadOnlySet<long> DependentIds(long permissionId)
    {
        var result = new HashSet<long>();
        var stack = new Stack<long>(_dependents.TryGetValue(permissionId, out var ids) ? ids : []);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!result.Add(current) || !_dependents.TryGetValue(current, out var next))
                continue;
            foreach (var item in next)
                stack.Push(item);
        }
        return result;
    }

    private async Task ToggleGlobalAsync(MatrixRow row)
    {
        try
        {
            await _permissionsApi.ToggleAsync(row.PermissionId, new TogglePermissionRequest(row.IsGloballyEnabled));
            _toast.Success(L["success"]);
            await LoadAsync();
        }
        catch
        {
            row.RevertGlobal();
            _toast.Error(L["error"]);
        }
    }
}
