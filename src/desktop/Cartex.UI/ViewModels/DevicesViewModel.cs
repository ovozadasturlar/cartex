using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class DevicesViewModel(ISessionsApi api, IDialogService dialog, IToastService toast, IBusyService busy, AuthService auth) : ViewModelBase, ILoadable
{
    public record DeviceRow(long Id, string Device, DateTime CreatedAt, DateTime LastUsedAt, DateTime ExpiresAt, string? Username);

    public ObservableCollection<DeviceRow> Sessions { get; } = [];

    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private bool _showAll;

    public bool CanViewAll => auth.HasPermission("users.manage");

    partial void OnShowAllChanged(bool value) => _ = LoadAsync();

    public async Task LoadAsync()
    {
        try
        {
            using (busy.Begin(L["loading"]))
            {
                var list = await api.GetSessionsAsync(ShowAll && CanViewAll);
                Sessions.Clear();
                foreach (var s in list)
                    Sessions.Add(new DeviceRow(s.Id, string.IsNullOrWhiteSpace(s.DeviceName) ? L["devices_unknown"] : s.DeviceName!,
                        s.CreatedAt, s.LastUsedAt, s.ExpiresAt, s.Username));
            }
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
        finally { IsEmpty = Sessions.Count == 0; }
    }

    [RelayCommand]
    private async Task RevokeAsync(DeviceRow? session)
    {
        if (session is null) return;
        if (!await dialog.ConfirmDangerAsync(L["devices_revoke_confirm"], L["revoke"])) return;
        try
        {
            using (busy.Begin(L["loading"]))
                await api.RevokeSessionAsync(session.Id);
            Sessions.Remove(session);
            IsEmpty = Sessions.Count == 0;
            toast.Success(L["success"]);
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
    }
}
