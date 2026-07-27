using Cartex.ApiClient.Api;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;
using Cartex.Shared.Models.Ordering;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QRCoder;

namespace Cartex.Mobile.Store.ViewModels;

public partial class HandoffViewModel(IOrderingApi orderingApi) : ObservableObject, IQueryAttributable
{
    [ObservableProperty] private string _code = "";
    [ObservableProperty] private ImageSource? _qrSource;
    [ObservableProperty] private string _status = "Open";
    [ObservableProperty] private string _statusText = Loc.Instance["status_open"];
    [ObservableProperty] private string _totalText = "";
    [ObservableProperty] private bool _isOpen = true;
    [ObservableProperty] private bool _isSold;

    private bool _soldNotified;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue("code", out var value)) return;
        Code = value.ToString() ?? "";
        var data = new QRCodeGenerator().CreateQrCode(Code, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data).GetGraphic(12);
        QrSource = ImageSource.FromStream(() => new MemoryStream(png));
    }

    public async Task PollAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var cart = await orderingApi.GetByCodeAsync(Code);
                Apply(cart.Status, cart.Total);
                if (Status is "CheckedOut" or "Cancelled") return;
            }
            catch { }
            try
            {
                await Task.Delay(5000, ct);
            }
            catch
            {
                return;
            }
        }
    }

    private void Apply(string status, decimal? total)
    {
        Status = status;
        if (total is { } t) TotalText = $"{t:N0} UZS";
        StatusText = Loc.Instance[status switch
        {
            "Confirmed" => "status_confirmed",
            "CheckedOut" => "status_checkedout",
            "Cancelled" => "status_cancelled",
            _ => "status_open"
        }];
        IsOpen = status == "Open";
        IsSold = status == "CheckedOut";
        if (IsSold && !_soldNotified)
        {
            _soldNotified = true;
            Ui.Toast(Loc.Instance["handoff_sold"]);
        }
    }

    [RelayCommand]
    private async Task CancelCartAsync()
    {
        var page = Shell.Current.CurrentPage;
        if (!await page.DisplayAlertAsync(Loc.Instance["cancel_cart"], Loc.Instance["cancel_cart_confirm"],
                Loc.Instance["yes"], Loc.Instance["no"])) return;
        try
        {
            await orderingApi.UpdateStatusAsync(Code, new UpdateCartStatusRequest("Cancelled"));
            Apply("Cancelled", null);
            Ui.Toast(Loc.Instance["cart_cancelled"]);
        }
        catch (Refit.ApiException ex)
        {
            await page.DisplayAlertAsync(Loc.Instance["error"], ApiErrors.Describe(ex), Loc.Instance["ok"]);
        }
        catch
        {
            Ui.Toast(Loc.Instance["err_no_connection"]);
        }
    }

}
