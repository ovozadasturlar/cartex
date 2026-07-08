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
    IHardwareKeysApi api, IUsersApi usersApi, IToastService toast, IBusyService busy) : ViewModelBase, ILoadable
{
    public ObservableCollection<UserDto> Users { get; } = [];

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
            }
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private void DetectDrive()
    {
        _drive = HardwareKeyReader.ScanForBlankDrive();
        DriveInfo = _drive is null ? L["no_drive_detected"] : $"{_drive.Root}  ·  {_drive.Serial}";
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
            }
            toast.Success(L["key_issued"]);
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
    }
}
