using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;
using Cartex.Shared.Models.Ordering;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QRCoder;
using Refit;

namespace Cartex.Mobile.Store.ViewModels;

public partial class HandoffViewModel(IOrderingApi orderingApi) : ObservableObject, IQueryAttributable
{
    public ObservableCollection<HandoffItemRow> Items { get; } = [];
    public ObservableCollection<CartParticipantDto> Participants { get; } = [];

    [ObservableProperty] private string _code = "";
    [ObservableProperty] private ImageSource? _qrSource;
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private string _totalText = "";
    [ObservableProperty] private string _customerName = "";
    [ObservableProperty] private string _note = "";
    [ObservableProperty] private string _claimedBy = "";
    [ObservableProperty] private string _cancellationReason = "";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _hasLoaded;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isOpen;
    [ObservableProperty] private bool _isConfirmed;
    [ObservableProperty] private bool _isSold;
    [ObservableProperty] private bool _isCancelled;
    [ObservableProperty] private bool _canShowQr;
    [ObservableProperty] private bool _canEdit;
    [ObservableProperty] private bool _canClaim;
    [ObservableProperty] private bool _canCheckout;
    [ObservableProperty] private bool _canCancel;
    [ObservableProperty] private bool _canOpenSale;
    [ObservableProperty] private bool _canRequeue;

    public bool HasCustomer => !string.IsNullOrWhiteSpace(CustomerName);
    public bool HasNote => !string.IsNullOrWhiteSpace(Note);
    public bool HasClaimant => !string.IsNullOrWhiteSpace(ClaimedBy);
    public bool HasCancellationReason => !string.IsNullOrWhiteSpace(CancellationReason);
    public bool HasParticipants => Participants.Count > 0;
    public bool HasError => !string.IsNullOrWhiteSpace(Error);

    private CartDto? _cart;
    private bool _soldNotified;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("code", out var value))
            Code = value.ToString() ?? "";
    }

    public async Task PollAsync(CancellationToken cancellationToken)
    {
        var first = true;
        while (!cancellationToken.IsCancellationRequested)
        {
            await LoadAsync(first, cancellationToken);
            first = false;
            if (HasLoaded && Status is "CheckedOut" or "Cancelled")
                return;

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync(true, CancellationToken.None);

    [RelayCommand]
    private Task EditAsync() => CanEdit
        ? Shell.Current.GoToAsync($"cart/edit?code={Uri.EscapeDataString(Code)}")
        : Task.CompletedTask;

    [RelayCommand]
    private async Task ClaimAsync()
    {
        if (!CanClaim || IsBusy) return;
        IsBusy = true;
        try
        {
            await orderingApi.UpdateStatusAsync(Code, new UpdateCartStatusRequest("Confirmed"));
            await LoadAsync(false, CancellationToken.None);
            await Shell.Current.GoToAsync($"checkout?code={Uri.EscapeDataString(Code)}");
        }
        catch (Exception ex)
        {
            Ui.Toast(Describe(ex));
            await LoadAsync(false, CancellationToken.None);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task CheckoutAsync() => CanCheckout
        ? Shell.Current.GoToAsync($"checkout?code={Uri.EscapeDataString(Code)}")
        : Task.CompletedTask;

    [RelayCommand]
    private Task OpenSaleAsync() => CanOpenSale && _cart?.SaleId is long saleId
        ? Shell.Current.GoToAsync($"sale/detail?id={saleId}")
        : Task.CompletedTask;

    [RelayCommand]
    private async Task RequeueAsync()
    {
        if (!CanRequeue || IsBusy) return;
        var page = Shell.Current.CurrentPage;
        if (!await page.DisplayAlertAsync(
                Loc.Instance["requeue_title"],
                Loc.Instance["requeue_confirm"],
                Loc.Instance["yes"],
                Loc.Instance["no"]))
            return;

        IsBusy = true;
        try
        {
            var result = await orderingApi.RequeueAsync(Code,
                new RequeueCartRequest(IdempotencyKey: Guid.NewGuid().ToString("N")));
            Code = result.AggregateCode;
            _soldNotified = false;
            await LoadAsync(false, CancellationToken.None);
            Ui.Toast(Loc.Instance["requeued"]);
        }
        catch (Exception ex)
        {
            Ui.Toast(Describe(ex));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CancelCartAsync()
    {
        if (!CanCancel || IsBusy) return;
        var page = Shell.Current.CurrentPage;
        if (!await page.DisplayAlertAsync(
                Loc.Instance["cart_cancel_title"],
                Loc.Instance["cart_cancel_confirm"],
                Loc.Instance["yes"],
                Loc.Instance["no"]))
            return;

        var reason = await page.DisplayPromptAsync(
            Loc.Instance["cart_cancel_title"],
            Loc.Instance["cancel_reason_optional"],
            Loc.Instance["apply"],
            Loc.Instance["later"]);

        IsBusy = true;
        try
        {
            await orderingApi.UpdateStatusAsync(Code,
                new UpdateCartStatusRequest("Cancelled", string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()));
            await LoadAsync(false, CancellationToken.None);
            Ui.Toast(Loc.Instance["cart_cancelled"]);
        }
        catch (Exception ex)
        {
            Ui.Toast(Describe(ex));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadAsync(bool showLoading, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Code) || IsLoading)
            return;

        IsLoading = true;
        if (showLoading && !HasLoaded)
            Error = null;
        try
        {
            var cart = await orderingApi.GetByCodeAsync(Code);
            Apply(cart);
            Error = null;
            HasLoaded = true;
        }
        catch (Exception ex)
        {
            Error = Describe(ex);
            if (!HasLoaded)
                ClearActions();
        }
        finally
        {
            IsLoading = false;
            NotifyDerived();
        }
    }

    private void Apply(CartDto cart)
    {
        _cart = cart;
        Status = cart.Status;
        StatusText = Loc.Instance[cart.Status switch
        {
            "Confirmed" => "status_confirmed",
            "CheckedOut" => "status_checkedout",
            "Cancelled" => "status_cancelled",
            _ => "status_open"
        }];
        TotalText = $"{cart.Total:N0} UZS";
        CustomerName = cart.CustomerName ?? "";
        Note = cart.Note ?? "";
        ClaimedBy = cart.ClaimedByName ?? "";
        CancellationReason = cart.CancellationReason ?? "";

        Items.Clear();
        foreach (var item in cart.Items)
            Items.Add(new HandoffItemRow(item));
        Participants.Clear();
        foreach (var participant in cart.Participants ?? [])
            Participants.Add(participant);

        IsOpen = cart.Status == "Open";
        IsConfirmed = cart.Status is "Confirmed" or "Ready";
        IsSold = cart.Status == "CheckedOut";
        IsCancelled = cart.Status == "Cancelled";

        var actions = cart.AllowedActions ?? [];
        CanShowQr = actions.Contains("showQr");
        CanEdit = actions.Contains("edit");
        CanClaim = actions.Contains("claim");
        CanCheckout = actions.Contains("checkout");
        CanCancel = actions.Contains("cancel");
        CanOpenSale = actions.Contains("openSale") && cart.SaleId.HasValue;
        CanRequeue = actions.Contains("requeue");
        QrSource = CanShowQr ? BuildQr(cart.AggregateCode) : null;

        if (IsSold && !_soldNotified)
        {
            _soldNotified = true;
            Ui.Toast(Loc.Instance["handoff_sold"]);
        }
        NotifyDerived();
    }

    private void ClearActions()
    {
        CanShowQr = CanEdit = CanClaim = CanCheckout = CanCancel = CanOpenSale = CanRequeue = false;
        QrSource = null;
    }

    private void NotifyDerived()
    {
        OnPropertyChanged(nameof(HasCustomer));
        OnPropertyChanged(nameof(HasNote));
        OnPropertyChanged(nameof(HasClaimant));
        OnPropertyChanged(nameof(HasCancellationReason));
        OnPropertyChanged(nameof(HasParticipants));
        OnPropertyChanged(nameof(HasError));
    }

    private static ImageSource BuildQr(string value)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(value, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data).GetGraphic(12);
        return ImageSource.FromStream(() => new MemoryStream(png));
    }

    private static string Describe(Exception exception) => exception is ApiException api
        ? ApiErrors.Describe(api)
        : Loc.Instance["err_no_connection"];
}

public sealed record HandoffItemRow(CartItemDto Item)
{
    public string Name => Item.ProductName;
    public string QuantityPrice => $"{Item.Quantity:0.###} × {Item.UnitPrice:N0}";
    public string Total => Item.LineTotal.ToString("N0");
}
