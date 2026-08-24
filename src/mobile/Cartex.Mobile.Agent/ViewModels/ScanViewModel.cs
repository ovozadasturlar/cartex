using Cartex.ApiClient.Api;
using Cartex.Mobile.Agent.Data;
using Cartex.Mobile.Agent.Services;
using Cartex.Shared.Models.Auth;
using CommunityToolkit.Mvvm.ComponentModel;
using Cartex.Mobile.Core;

namespace Cartex.Mobile.Agent.ViewModels;

public partial class ScanViewModel(ISessionsApi sessionsApi, AgentDb db, CartService cart, SessionStore session, MobileAuthService auth, AccessState access) : ObservableObject
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
        else if (QrActions.TryServer(value, out var serverUrl))
        {
            await ConnectServerAsync(serverUrl);
            Reset();
            return;
        }
        else if (QrActions.IsWifi(value))
        {
            await QrActions.HandleWifiAsync(value);
            Reset();
            return;
        }
        else if (await db.FindByBarcodeAsync(value) is { } stock)
        {
            cart.Add(stock, 1);
            Ui.Haptic();
            Ui.Toast(string.Format(Loc.Instance["added_to_cart_fmt"], stock.ProductName));
            await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync(".."));
            return;
        }
        else if (QrActions.IsHttpUrl(value))
        {
            await QrActions.OpenLinkAsync(value);
            Reset();
            return;
        }
        else
        {
            Status = Loc.Instance["err_barcode_unknown"];
        }

        await Task.Delay(1800);
        Reset();
    }

    private void Reset()
    {
        Status = Loc.Instance["scan_hint"];
        _handled = false;
        IsDetecting = true;
    }

    private async Task ConnectServerAsync(string url)
    {
        var page = Shell.Current.CurrentPage;
        if (page is null) return;
        if (QrActions.IsSameServer(url, session.ServerUrl))
        {
            Ui.Toast(Loc.Instance["server_already_connected"]);
            return;
        }
        if (await db.CountOutboxAsync("pending") + await db.CountOutboxAsync("error") > 0)
        {
            await page.DisplayAlertAsync(Loc.Instance["server"], Loc.Instance["server_switch_pending"], Loc.Instance["ok"]);
            return;
        }
        if (!await page.DisplayAlertAsync(
                Loc.Instance["server"], string.Format(Loc.Instance["server_connect_confirm"], url),
                Loc.Instance["yes"], Loc.Instance["no"]))
            return;
        if (!await QrActions.ProbeServerAsync(url))
        {
            await page.DisplayAlertAsync(Loc.Instance["server"], Loc.Instance["server_unreachable"], Loc.Instance["ok"]);
            return;
        }
        // Eski do'kon nusxasi yangi serverda ishlatilmasligi kerak; navbat bo'sh ekani
        // yuqorida tekshirilgani uchun bu yerda hech narsa yo'qolmaydi.
        AppLock.Disable();
        await auth.LogoutAsync();
        access.Clear();
        await db.ClearCacheAsync();
        cart.Clear();
        session.ServerUrl = url;
        await Shell.Current.GoToAsync("//login");
        Ui.Toast(Loc.Instance["server_switch_login"]);
    }
}
