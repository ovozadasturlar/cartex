using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Roles;
using Cartex.Shared.Models.Permissions;

namespace Cartex.UI.ViewModels;

public partial class RolesViewModel : ViewModelBase
{
    private readonly IRolesApi _rolesApi;
    private readonly IPermissionsApi _permissionsApi;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private RoleDto? _selectedRole;

    public ObservableCollection<RoleDto> Roles { get; } = [];
    public ObservableCollection<PermissionDto> AllPermissions { get; } = [];

    public RolesViewModel(IRolesApi rolesApi, IPermissionsApi permissionsApi)
    {
        _rolesApi = rolesApi;
        _permissionsApi = permissionsApi;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var roles = await _rolesApi.GetAllAsync();
            Roles.Clear();
            foreach (var r in roles)
                Roles.Add(r);

            var perms = await _permissionsApi.GetAllAsync();
            AllPermissions.Clear();
            foreach (var p in perms)
                AllPermissions.Add(p);
        }
        catch { }
        finally
        {
            IsLoading = false;
        }
    }
}
