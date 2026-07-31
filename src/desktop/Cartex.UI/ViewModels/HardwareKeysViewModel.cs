using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Auth;
using Cartex.Shared.Models.Users;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class HardwareKeysViewModel(
    IHardwareKeysApi api,
    IUsersApi usersApi,
    IDialogService dialog,
    IToastService toast,
    IBusyService busy,
    AuthService auth) : ViewModelBase, ILoadable
{
    public sealed record KeyGroup(string Serial, IReadOnlyList<HardwareKeyDto> Items);

    public ObservableCollection<UserDto> Users { get; } = [];
    public ObservableCollection<KeyGroup> KeyGroups { get; } = [];

    [ObservableProperty] private UserDto? _selectedUser;
    [ObservableProperty] private string? _driveInfo;
    public ObservableCollection<DetectedDrive> Drives { get; } = [];
    [ObservableProperty] private DetectedDrive? _selectedDrive;
    public bool HasMultipleDrives => Drives.Count > 1;
    public bool CanCreate => auth.HasPermission("keys.create");
    public bool CanEdit => auth.HasPermission("keys.edit");
    public bool CanRevoke => auth.HasPermission("keys.revoke");

    public async Task LoadAsync()
    {
        try
        {
            using (busy.Begin(L["loading"]))
            {
                if (CanCreate)
                {
                    var list = await usersApi.GetAllAsync();
                    Users.Clear();
                    foreach (var u in list.Where(u => u.IsActive))
                        Users.Add(u);
                }
                await ReloadKeysAsync();
            }
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
    }

    private async Task ReloadKeysAsync()
    {
        var keys = await api.GetAllAsync();
        KeyGroups.Clear();
        foreach (var g in keys.GroupBy(k => k.Serial))
            KeyGroups.Add(new KeyGroup(g.Key, [.. g]));
    }

    [RelayCommand]
    private async Task RevokeKeyAsync(HardwareKeyDto? key)
    {
        if (!CanRevoke || key is null || key.RevokedAt is not null) return;
        if (!await dialog.ConfirmDangerAsync(L["key_revoke_confirm"], L["revoke"])) return;
        try
        {
            using (busy.Begin(L["loading"]))
            {
                await api.RevokeAsync(key.Id);
                await ReloadKeysAsync();
            }
            toast.Success(L["success"]);
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task ToggleKeyAsync(HardwareKeyDto? key)
    {
        if (!CanEdit || key is null || key.RevokedAt is not null) return;
        try
        {
            using (busy.Begin(L["loading"]))
            {
                await api.SetEnabledAsync(key.Id, new SetHardwareKeyEnabledRequest(!key.IsEnabled));
                await ReloadKeysAsync();
            }
            toast.Success(L["success"]);
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private void DetectDrive()
    {
        Drives.Clear();
        foreach (var d in HardwareKeyReader.ScanForDrives()) Drives.Add(d);
        OnPropertyChanged(nameof(HasMultipleDrives));
        SelectedDrive = Drives.FirstOrDefault();
        if (SelectedDrive is null) DriveInfo = L["no_drive_detected"];
    }

    partial void OnSelectedDriveChanged(DetectedDrive? value)
    {
        if (value is null) return;
        var keyCount = HardwareKeyReader.CountKeys(value.Root);
        DriveInfo = $"{value.Root}  ·  {value.Serial}"
            + (keyCount > 0 ? $"  ·  {string.Format(L["key_drive_has_keys"], keyCount)}" : "");
    }

    [RelayCommand]
    private async Task IssueKeyAsync()
    {
        if (!CanCreate) return;
        if (SelectedUser is null) { toast.Warning(L["error"]); return; }
        if (SelectedDrive is not { } drive) { toast.Warning(L["no_drive_detected"]); return; }
        try
        {
            using (busy.Begin(L["loading"]))
            {
                var result = await api.GenerateAsync(new GenerateHardwareKeyRequest(SelectedUser.Id, drive.Serial));
                await File.WriteAllTextAsync(Path.Combine(drive.Root, result.FileName), result.Content);
                await ReloadKeysAsync();
            }
            OnSelectedDriveChanged(drive);
            toast.Success(L["key_issued"]);
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
    }
}
