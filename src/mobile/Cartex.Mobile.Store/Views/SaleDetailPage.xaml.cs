using Cartex.ApiClient.Api;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;
using Cartex.Mobile.Store.ViewModels;
using Cartex.Shared.Models.Sales;
using Refit;

namespace Cartex.Mobile.Store.Views;

public partial class SaleDetailPage : ContentPage, IQueryAttributable
{
    private readonly SaleDetailViewModel _viewModel;
    private readonly ISalesApi _salesApi;
    private bool _hasAppeared;
    private bool _sendingSms;
    private long _saleId;

    public SaleDetailPage(SaleDetailViewModel viewModel, ISalesApi salesApi)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _salesApi = salesApi;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var value))
            long.TryParse(value.ToString(), out _saleId);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_hasAppeared)
            await _viewModel.ReloadAsync();
        else
        {
            _hasAppeared = true;
            await _viewModel.AppearAsync();
        }
        SmsMissingReason.IsVisible = _viewModel.CanResend && string.IsNullOrWhiteSpace(_viewModel.CustomerPhone);
    }

    private async void SendReceiptSmsClicked(object? sender, EventArgs e)
    {
        if (_sendingSms || _saleId <= 0 || string.IsNullOrWhiteSpace(_viewModel.CustomerPhone))
            return;
        _sendingSms = true;
        SendReceiptSmsButton.IsEnabled = false;
        try
        {
            var preview = await _salesApi.GetReceiptSmsPreviewAsync(_saleId);
            if (!await DisplayAlertAsync(
                    Loc.Instance["send_receipt_sms"],
                    string.Format(Loc.Instance["receipt_sms_confirm"], preview.Recipient, preview.Text),
                    Loc.Instance["send"],
                    Loc.Instance["cancel"]))
                return;

            await _salesApi.SendReceiptSmsAsync(_saleId, new SendReceiptSmsRequest(preview.ConfirmationToken));
            Ui.Toast(Loc.Instance["receipt_sms_queued"]);
        }
        catch (Exception exception)
        {
            Ui.Toast(exception is ApiException api ? ApiErrors.Describe(api) : Loc.Instance["err_no_connection"]);
        }
        finally
        {
            _sendingSms = false;
            SendReceiptSmsButton.IsEnabled = !string.IsNullOrWhiteSpace(_viewModel.CustomerPhone);
        }
    }
}
