using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Querying;
using Cartex.ApiClient.Paging;
using Cartex.Shared.Models.Users;
using Cartex.Shared.Models.Roles;
using Cartex.Shared.Models.Branches;
using Cartex.UI.Models;
using Cartex.UI.Services;
using Cartex.UI.ViewModels.Common;

namespace Cartex.UI.ViewModels;

public partial class UsersViewModel : ViewModelBase, ILoadable
{
    private readonly IUsersApi _usersApi;
    private readonly IRolesApi _rolesApi;
    private readonly IBranchesApi _branchesApi;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private readonly IExportService _export;
    private readonly AuthService _auth;
    private readonly IDialogService _dialog;

    private List<RoleDto> _allRoles = [];
    private List<BranchDto> _allBranches = [];
    private long _editId;

    public ObservableCollection<UserDto> Users { get; } = [];
    public PaginationState Paging { get; } = new();
    public ObservableCollection<SelectItem> EditRoles { get; } = [];
    public ObservableCollection<SelectItem> EditBranches { get; } = [];
    public ObservableCollection<BranchOption> DefaultBranches { get; } = [];
    public ObservableCollection<StartPageOption> StartPages { get; } = [];
    public ObservableCollection<StartPageOption> CartDestinations { get; } = [];

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private bool _isEditOpen;
    [ObservableProperty] private bool _isNew;
    [ObservableProperty] private string _editFullName = "";
    [ObservableProperty] private string _editUsername = "";
    [ObservableProperty] private string _editPassword = "";
    [ObservableProperty] private bool _editIsActive = true;
    [ObservableProperty] private BranchOption? _selectedDefaultBranch;
    [ObservableProperty] private StartPageOption? _selectedStartPage;
    [ObservableProperty] private StartPageOption? _selectedCartDestination;

    public bool IsEmpty => Users.Count == 0;
    public bool CanExport => _auth.HasPermission("reports.export");
    public bool CanCreate => _auth.HasPermission("users.create");
    public bool CanEdit => _auth.HasPermission("users.edit");
    public bool CanDelete => _auth.HasPermission("users.delete");
    public bool CanChangeUsername => _auth.HasPermission("*");
    public string EditTitle => IsNew ? L["create_user"] : L["edit_user"];
    public string PasswordLabel => IsNew ? L["password"] : L["new_password"];

    [ObservableProperty] private bool _isChangeUsernameOpen;
    [ObservableProperty] private string _changeUsernameTarget = "";
    [ObservableProperty] private string _newUsername = "";
    private long _changeUsernameId;

    private IReadOnlyList<PageShortcut>? _shortcuts;
    public IReadOnlyList<PageShortcut> Shortcuts => _shortcuts ??= CrudShortcuts(OpenCreateCommand, SaveCommand, () => IsEditOpen = false, () => IsEditOpen);

    public UsersViewModel(IUsersApi usersApi, IRolesApi rolesApi, IBranchesApi branchesApi, IToastService toast, IBusyService busy, IExportService export, AuthService auth, IDialogService dialog)
    {
        _usersApi = usersApi;
        _rolesApi = rolesApi;
        _branchesApi = branchesApi;
        _toast = toast;
        _busy = busy;
        _export = export;
        _auth = auth;
        _dialog = dialog;
        Paging.Attach(LoadUsersAsync);
        Paging.ConfigureSort([new(L["full_name"], "FullName"), new(L["username"], "Username"), new(L["date"], "CreatedAt")]);
    }

    [RelayCommand]
    private async Task Export(string format)
    {
        try
        {
            var all = await _usersApi.GetAllAsync();
            await _export.ExportAsync(L["users"], all,
            [
                new(L["full_name"], u => u.FullName),
                new(L["username"], u => u.Username),
                new(L["roles"], u => string.Join(", ", u.RoleNames)),
                new(L["default_branch"], u => u.DefaultBranchName),
                new(L["active"], u => u.IsActive),
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
                _allRoles = await _rolesApi.GetAllAsync();
                _allBranches = await _branchesApi.GetAllAsync();
                BuildStartPages();
                BuildCartDestinations();
                await LoadUsersAsync();
            }
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    private async Task LoadUsersAsync()
    {
        try
        {
            var search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim();
            var result = await _usersApi.QueryAsync(QueryRequest.Create()
                .Page(Paging.Page, Paging.PageSize)
                .Sort(Paging.SortBy, Paging.Descending)
                .Search(search)
                .Build());
            var paged = result.ToPaged();
            Users.Clear();
            foreach (var u in paged.Items) Users.Add(u);
            Paging.Apply(paged.Meta);
            OnPropertyChanged(nameof(IsEmpty));
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    partial void OnSearchTextChanged(string value) { Paging.Page = 1; _ = LoadUsersAsync(); }

    private void BuildStartPages()
    {
        StartPages.Clear();
        StartPages.Add(new StartPageOption(null, L["none"]));
        // RUXSAT-04: moduli o'chiq sahifa boshlang'ich sahifa sifatida taklif qilinmaydi — aks holda
        // foydalanuvchi kirgan zahoti ko'rinmaydigan bo'limga tushirilardi.
        foreach (var p in NavRegistry.SidebarPages.Where(x => x.Feature is null || NavRegistry.IsFeatureOn(x.Feature)))
            StartPages.Add(new StartPageOption(p.Key, L[p.Key]));
    }

    private void BuildCartDestinations()
    {
        CartDestinations.Clear();
        CartDestinations.Add(new StartPageOption(null, "—"));
        CartDestinations.Add(new StartPageOption("queue", L["dest_queue"]));
        CartDestinations.Add(new StartPageOption("order", L["dest_order"]));
    }

    private void BuildDefaultBranches()
    {
        DefaultBranches.Clear();
        DefaultBranches.Add(new BranchOption(null, L["none"]));
        foreach (var b in _allBranches)
            DefaultBranches.Add(new BranchOption(b.Id, b.Name));
    }

    private void BuildRoleItems(IReadOnlyCollection<long> selected)
    {
        EditRoles.Clear();
        foreach (var r in _allRoles)
        {
            var isSelected = selected.Contains(r.Id);
            EditRoles.Add(new SelectItem
            {
                Id = r.Id,
                Label = r.IsActive ? r.Name : $"{r.Name} · {L["inactive"]}",
                IsSelected = isSelected,
                IsEnabled = r.IsActive || isSelected
            });
        }
    }

    private void BuildBranchItems(IReadOnlyCollection<long> selected)
    {
        EditBranches.Clear();
        foreach (var b in _allBranches)
            EditBranches.Add(new SelectItem { Id = b.Id, Label = b.Name, IsSelected = selected.Contains(b.Id) });
    }

    [RelayCommand]
    private void OpenCreate()
    {
        if (!CanCreate) return;
        IsNew = true;
        _editId = 0;
        EditFullName = "";
        EditUsername = "";
        EditPassword = "";
        EditIsActive = true;
        BuildRoleItems([]);
        BuildBranchItems([]);
        BuildDefaultBranches();
        SelectedDefaultBranch = DefaultBranches.FirstOrDefault();
        SelectedStartPage = StartPages.FirstOrDefault();
        SelectedCartDestination = CartDestinations.FirstOrDefault();
        OnPropertyChanged(nameof(EditTitle));
        OnPropertyChanged(nameof(PasswordLabel));
        IsEditOpen = true;
    }

    [RelayCommand]
    private void OpenEdit(UserDto user)
    {
        if (!CanEdit) return;
        IsNew = false;
        _editId = user.Id;
        EditFullName = user.FullName;
        EditUsername = user.Username;
        EditPassword = "";
        EditIsActive = user.IsActive;
        BuildRoleItems(user.RoleIds);
        BuildBranchItems(user.BranchIds);
        BuildDefaultBranches();
        SelectedDefaultBranch = DefaultBranches.FirstOrDefault(b => b.Id == user.DefaultBranchId) ?? DefaultBranches.FirstOrDefault();
        SelectedStartPage = StartPages.FirstOrDefault(s => s.Key == user.StartPage) ?? StartPages.FirstOrDefault();
        SelectedCartDestination = CartDestinations.FirstOrDefault(s => s.Key == user.CartDestination) ?? CartDestinations.FirstOrDefault();
        OnPropertyChanged(nameof(EditTitle));
        OnPropertyChanged(nameof(PasswordLabel));
        IsEditOpen = true;
    }

    [RelayCommand]
    private void CancelEdit() => IsEditOpen = false;

    [RelayCommand]
    private void OpenChangeUsername(UserDto user)
    {
        if (!CanChangeUsername) return;
        _changeUsernameId = user.Id;
        ChangeUsernameTarget = $"Joriy username: {user.Username}";
        NewUsername = user.Username;
        IsChangeUsernameOpen = true;
    }

    [RelayCommand]
    private void CancelChangeUsername() => IsChangeUsernameOpen = false;

    [RelayCommand]
    private async Task ConfirmChangeUsernameAsync()
    {
        if (string.IsNullOrWhiteSpace(NewUsername)) { _toast.Error(L["error"]); return; }
        try
        {
            using (_busy.Begin(L["loading"]))
                await _usersApi.ChangeUsernameAsync(_changeUsernameId, new ChangeUsernameRequest(NewUsername.Trim()));
            _toast.Success(L["success"]);
            IsChangeUsernameOpen = false;
            await LoadAsync();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task DeleteAsync(UserDto user)
    {
        if (!CanDelete) return;
        if (!await _dialog.ConfirmDangerAsync(string.Format(L["delete_user_confirm"], user.Username), L["delete"]))
            return;
        try
        {
            using (_busy.Begin(L["loading"]))
                await _usersApi.DeleteAsync(user.Id);
            _toast.Success(L["success"]);
            await LoadAsync();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var roleIds = EditRoles.Where(r => r.IsSelected).Select(r => r.Id).ToList();
        if (string.IsNullOrWhiteSpace(EditFullName) || string.IsNullOrWhiteSpace(EditUsername) || roleIds.Count == 0)
        {
            _toast.Error(L["error"]);
            return;
        }

        var branchIds = EditBranches.Where(b => b.IsSelected).Select(b => b.Id).ToList();
        var defaultBranchId = SelectedDefaultBranch?.Id;
        var selectedRoles = _allRoles.Where(role => roleIds.Contains(role.Id)).ToList();
        var requiresBranch = selectedRoles.Any(role => role.RequiresBranch);
        var canAccessAllBranches = selectedRoles.Any(role =>
            role.AccessAll || role.Permissions.Contains("branch.viewAll"));
        if (requiresBranch && branchIds.Count == 0 && !canAccessAllBranches)
        {
            _toast.Error(L["branch_required_for_role"]);
            return;
        }
        if (branchIds.Count == 1 && defaultBranchId is null)
            defaultBranchId = branchIds[0];
        if (requiresBranch && branchIds.Count > 1 && defaultBranchId is null)
        {
            _toast.Error(L["default_branch_required"]);
            return;
        }
        if (defaultBranchId is not null && !canAccessAllBranches && !branchIds.Contains(defaultBranchId.Value))
        {
            _toast.Error(L["default_branch_must_be_accessible"]);
            return;
        }
        var startPage = SelectedStartPage?.Key;
        var cartDestination = SelectedCartDestination?.Key;

        try
        {
            using (_busy.Begin(L["loading"]))
            {
                if (IsNew)
                {
                    if (string.IsNullOrWhiteSpace(EditPassword)) { _toast.Error(L["error"]); return; }
                    await _usersApi.CreateAsync(new CreateUserRequest(
                        EditFullName.Trim(), EditUsername.Trim(), EditPassword, roleIds, defaultBranchId, branchIds, startPage, CartDestination: cartDestination));
                }
                else
                {
                    var newPassword = string.IsNullOrWhiteSpace(EditPassword) ? null : EditPassword;
                    await _usersApi.UpdateAsync(_editId, new UpdateUserRequest(
                        EditFullName.Trim(), roleIds, EditIsActive, newPassword, defaultBranchId, branchIds, startPage, CartDestination: cartDestination));
                }
            }
            IsEditOpen = false;
            _toast.Success(L["success"]);
            await LoadAsync();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }
}

public partial class SelectItem : ObservableObject
{
    public long Id { get; init; }
    public string Label { get; init; } = "";
    public bool IsEnabled { get; init; } = true;
    [ObservableProperty] private bool _isSelected;
}

public record BranchOption(long? Id, string Name);
