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
    IHardwareKeysApi api, IUsersApi usersApi, IDialogService dialog, IToastService toast, IBusyService busy) : ViewModelBase, ILoadable
{
    public ObservableCollection<UserDto> Users { get; } = [];
    public ObservableCollection<HardwareKeyDto> Keys { get; } = [];

    [ObservableProperty] private UserDto? _selectedUser;
    [ObservableProperty] private string? _driveInfo;
    private DetectedDrive? _drive;

    public async Task LoadAsync()
    {
        try
        {
            using (busy.Begin(L["loading"]))
            {
                var list = await usersApi.GetAllAsync();
                Users.Clear();
                foreach (var u in list.Where(u => u.IsActive))
                    Users.Add(u);
                await ReloadKeysAsync();
            }
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
    }

    private async Task ReloadKeysAsync()
    {
        var keys = await api.GetAllAsync();
        Keys.Clear();
        foreach (var k in keys) Keys.Add(k);
    }

    [RelayCommand]
    private async Task RevokeKeyAsync(HardwareKeyDto? key)
    {
        if (key is null || key.RevokedAt is not null) return;
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
        if (key is null || key.RevokedAt is not null) return;
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
        _drive = HardwareKeyReader.ScanForBlankDrive();
        if (_drive is null) { DriveInfo = L["no_drive_detected"]; return; }
        var keyCount = HardwareKeyReader.CountKeys(_drive.Root);
        DriveInfo = $"{_drive.Root}  ·  {_drive.Serial}"
            + (keyCount > 0 ? $"  ·  {string.Format(L["key_drive_has_keys"], keyCount)}" : "");
    }

    [RelayCommand]
    private async Task IssueKeyAsync()
    {
        if (SelectedUser is null) { toast.Warning(L["error"]); return; }
        if (_drive is null) { toast.Warning(L["no_drive_detected"]); return; }
        try
        {
            using (busy.Begin(L["loading"]))
            {
                var result = await api.GenerateAsync(new GenerateHardwareKeyRequest(SelectedUser.Id, _drive.Serial));
                await File.WriteAllTextAsync(Path.Combine(_drive.Root, result.FileName), result.Content);
                await ReloadKeysAsync();
            }
            toast.Success(L["key_issued"]);
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
    }
}
