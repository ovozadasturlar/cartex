using Cartex.ApiClient.Api;
using Cartex.Mobile.Agent.Services;
using Cartex.Shared.Models.Auth;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Cartex.Mobile.Agent.ViewModels;

public partial class ScanViewModel(ISessionsApi sessionsApi) : ObservableObject
{
    [ObservableProperty] private bool _isDetecting = true;
    [ObservableProperty] private string? _status = Loc.Instance["scan_hint"];

    private bool _handled;

    public async Task HandleAsync(string value)
    {
        if (_handled) return;
        _handled = true;
        IsDetecting = false;

        if (value.StartsWith("cartexqr:", StringComparison.OrdinalIgnoreCase))
        {
            var code = value["cartexqr:".Length..];
            try
            {
                Status = Loc.Instance["approving"];
                await sessionsApi.ApproveQrAsync(new ApproveQrLoginRequest(code));
                Ui.Toast(Loc.Instance["qr_approved"]);
                await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync(".."));
                return;
            }
            catch (Refit.ApiException ex)
            {
                Status = SyncService.DescribeError(ex);
            }
            catch
            {
                Status = Loc.Instance["err_no_connection"];
            }
        }
        else
        {
            Status = Loc.Instance["not_cartex_qr"];
        }

        await Task.Delay(1800);
        Status = Loc.Instance["scan_hint"];
        _handled = false;
        IsDetecting = true;
    }
}
