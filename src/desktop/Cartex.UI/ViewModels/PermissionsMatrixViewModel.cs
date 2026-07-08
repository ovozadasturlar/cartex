using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
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

    partial void OnIsAssignedChanged(bool value)
    {
        if (!_suppress) _ = _onToggled(this);
    }
}

public partial class MatrixRow : ObservableObject
{
    public long PermissionId { get; init; }
    public string Name { get; init; } = "";
    public string? Description { get; init; }
    public bool CanGovern { get; init; }
    public ObservableCollection<MatrixCell> Cells { get; } = [];

    private readonly Func<MatrixRow, Task> _onGlobalToggled;
    private bool _suppress;

    [ObservableProperty] private bool _isGloballyEnabled;

    public MatrixRow(Func<MatrixRow, Task> onGlobalToggled) => _onGlobalToggled = onGlobalToggled;

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

    partial void OnIsGloballyEnabledChanged(bool value)
    {
        if (!_suppress) _ = _onGlobalToggled(this);
    }
}

public class MatrixGroup
{
    public string Title { get; init; } = "";
    public ObservableCollection<MatrixRow> Rows { get; } = [];
}

public partial class PermissionsMatrixViewModel : ViewModelBase, ILoadable
{
    private readonly IRolesApi _rolesApi;
    private readonly IPermissionsApi _permissionsApi;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private readonly AuthService _auth;

    private readonly Dictionary<long, HashSet<long>> _rolePermissionIds = [];

    public ObservableCollection<RoleColumn> Roles { get; } = [];
    public ObservableCollection<MatrixGroup> Groups { get; } = [];

    public bool CanGovern => _auth.HasPermission("permissions.govern");
    public bool IsEmpty => Groups.Count == 0 || Roles.Count == 0;

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

                _rolePermissionIds.Clear();
                foreach (var role in roles)
                    _rolePermissionIds[role.Id] = role.Permissions
                        .Where(nameToId.ContainsKey).Select(n => nameToId[n]).ToHashSet();

                Roles.Clear();
                foreach (var role in roles) Roles.Add(new RoleColumn(role.Id, role.Name));

                Groups.Clear();
                foreach (var group in permissions.OrderBy(p => p.Name).GroupBy(p => p.Name.Split('.')[0]).OrderBy(g => g.Key))
                {
                    var g = new MatrixGroup { Title = LocalizationManager.Instance.Find($"perm_group_{group.Key}") ?? group.Key };
                    foreach (var perm in group)
                    {
                        var row = new MatrixRow(ToggleGlobalAsync)
                        {
                            PermissionId = perm.Id,
                            Name = LocalizationManager.Instance.Find($"perm_{perm.Name}") ?? perm.Description ?? perm.Name,
                            Description = perm.Name,
                            CanGovern = CanGovern
                        };
                        row.SetEnabled(perm.IsEnabled);
                        foreach (var role in roles)
                            row.Cells.Add(new MatrixCell(role.Id, perm.Id,
                                _rolePermissionIds[role.Id].Contains(perm.Id), perm.IsEnabled, ToggleCellAsync));
                        g.Rows.Add(row);
                    }
                    Groups.Add(g);
                }

                OnPropertyChanged(nameof(IsEmpty));
            }
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    private async Task ToggleCellAsync(MatrixCell cell)
    {
        var set = _rolePermissionIds[cell.RoleId];
        if (cell.IsAssigned) set.Add(cell.PermissionId); else set.Remove(cell.PermissionId);
        try
        {
            await _rolesApi.AssignPermissionsAsync(cell.RoleId, new AssignPermissionsRequest(set.ToList()));
            _toast.Success(L["success"]);
        }
        catch
        {
            if (cell.IsAssigned) set.Remove(cell.PermissionId); else set.Add(cell.PermissionId);
            cell.Revert();
            _toast.Error(L["error"]);
        }
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
