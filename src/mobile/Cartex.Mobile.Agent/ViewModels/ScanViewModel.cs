using Cartex.ApiClient.Api;
using Cartex.Mobile.Agent.Data;
using Cartex.Mobile.Agent.Services;
using Cartex.Shared.Models.Auth;
using CommunityToolkit.Mvvm.ComponentModel;
using Cartex.Mobile.Core;

namespace Cartex.Mobile.Agent.ViewModels;

public partial class ScanViewModel(ISessionsApi sessionsApi, AgentDb db, CartService cart) : ObservableObject
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
        else if (await db.FindByBarcodeAsync(value) is { } stock)
        {
            cart.Add(stock, 1);
            Ui.Haptic();
            Ui.Toast(string.Format(Loc.Instance["added_to_cart_fmt"], stock.ProductName));
            await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync(".."));
            return;
        }
        else
        {
            Status = Loc.Instance["err_barcode_unknown"];
        }

        await Task.Delay(1800);
        Status = Loc.Instance["scan_hint"];
        _handled = false;
        IsDetecting = true;
    }
}
