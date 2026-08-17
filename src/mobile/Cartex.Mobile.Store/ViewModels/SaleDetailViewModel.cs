using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;
using Cartex.Shared.Models.Sales;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Refit;

namespace Cartex.Mobile.Store.ViewModels;

public partial class SaleDetailViewModel(
    ISalesApi salesApi,
    MobilePrintDispatcher printDispatcher) : ObservableObject, IQueryAttributable
{
    public ObservableCollection<SaleDetailItemRow> Items { get; } = [];
    public ObservableCollection<SaleDetailPaymentRow> Payments { get; } = [];
    public ObservableCollection<SaleDetailParticipantDto> Participants { get; } = [];
    public ObservableCollection<SaleReturnSummaryDto> Returns { get; } = [];

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isLoaded;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string _dateText = "";
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private string _customerName = "";
    [ObservableProperty] private string _customerPhone = "";
    [ObservableProperty] private string _cashierText = "";
    [ObservableProperty] private string _locationText = "";
    [ObservableProperty] private string _totalText = "";
    [ObservableProperty] private string _discountText = "";
    [ObservableProperty] private string _debtText = "";
    [ObservableProperty] private string _changeText = "";
    [ObservableProperty] private bool _canPrint;
    [ObservableProperty] private bool _canResend;
    [ObservableProperty] private bool _canOpenCustomer;
    [ObservableProperty] private bool _canCreateReturn;

    public bool HasCustomer => !string.IsNullOrWhiteSpace(CustomerName);
    public bool HasDiscount => !string.IsNullOrWhiteSpace(DiscountText);
    public bool HasDebt => !string.IsNullOrWhiteSpace(DebtText);
    public bool HasChange => !string.IsNullOrWhiteSpace(ChangeText);
    public bool HasPayments => Payments.Count > 0;
    public bool HasParticipants => Participants.Count > 0;
    public bool HasReturns => Returns.Count > 0;
    public bool HasError => !string.IsNullOrWhiteSpace(Error);

    private long _saleId;
    private SaleDetailDto? _sale;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var value))
            long.TryParse(value.ToString(), out _saleId);
    }

    public async Task AppearAsync()
    {
        if (IsLoaded || IsLoading)
            return;
        await LoadAsync();
    }

    public Task ReloadAsync() => LoadAsync();

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    private async Task PrintAsync()
    {
        if (_sale is null || !CanPrint || IsBusy) return;
        IsBusy = true;
        try
        {
            await printDispatcher.ReprintReceiptAsync(_sale.Id, _sale.ReceiptToken);
            Ui.Toast(Loc.Instance["print_sent"]);
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
    private async Task ResendAsync()
    {
        if (_sale is null || !CanResend || IsBusy) return;
        IsBusy = true;
        try
        {
            await salesApi.ResendReceiptAsync(_sale.Id);
            Ui.Toast(Loc.Instance["receipt_resent"]);
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
    private Task OpenCustomerAsync() => _sale?.CustomerId is long customerId && CanOpenCustomer
        ? Shell.Current.GoToAsync($"customer/detail?id={customerId}")
        : Task.CompletedTask;

    [RelayCommand]
    private Task CreateReturnAsync() => _sale is not null && CanCreateReturn
        ? Shell.Current.GoToAsync($"sale/return?id={_sale.Id}")
        : Task.CompletedTask;

    private async Task LoadAsync()
    {
        if (_saleId <= 0 || IsLoading) return;
        IsLoading = true;
        Error = null;
        try
        {
            var sale = _sale = await salesApi.GetByIdAsync(_saleId);
            var localDate = sale.SaleDate.Kind == DateTimeKind.Utc ? sale.SaleDate.ToLocalTime() : sale.SaleDate;
            DateText = localDate.ToString("dd.MM.yyyy HH:mm");
            Status = sale.Status;
            CustomerName = sale.CustomerName ?? "";
            CustomerPhone = sale.CustomerPhone ?? "";
            CashierText = sale.UserName;
            LocationText = $"{sale.BranchName} · {sale.WarehouseName}";
            TotalText = $"{sale.TotalAmount:N0} UZS";
            DiscountText = sale.DiscountAmount > 0 ? $"{sale.DiscountAmount:N0} UZS" : "";
            DebtText = sale.DebtAmount > 0 ? $"{sale.DebtAmount:N0} {sale.DebtCurrency}" : "";
            ChangeText = sale.ChangeAmount > 0 ? $"{sale.ChangeAmount:N0} UZS" : "";

            Items.Clear();
            foreach (var item in sale.Items)
                Items.Add(new SaleDetailItemRow(item));
            Payments.Clear();
            foreach (var payment in sale.Payments)
                Payments.Add(new SaleDetailPaymentRow(payment));
            Participants.Clear();
            foreach (var participant in sale.Participants)
                Participants.Add(participant);
            Returns.Clear();
            foreach (var item in sale.Returns)
                Returns.Add(item);

            CanPrint = sale.AllowedActions.Contains("printReceipt") || sale.AllowedActions.Contains("reprintReceipt");
            CanResend = sale.AllowedActions.Contains("resendReceipt");
            CanOpenCustomer = sale.AllowedActions.Contains("openCustomer");
            CanCreateReturn = sale.AllowedActions.Contains("createReturn");
            IsLoaded = true;
        }
        catch (Exception ex)
        {
            Error = Describe(ex);
        }
        finally
        {
            IsLoading = false;
            NotifyDerived();
        }
    }

    private void NotifyDerived()
    {
        OnPropertyChanged(nameof(HasCustomer));
        OnPropertyChanged(nameof(HasDiscount));
        OnPropertyChanged(nameof(HasDebt));
        OnPropertyChanged(nameof(HasChange));
        OnPropertyChanged(nameof(HasPayments));
        OnPropertyChanged(nameof(HasParticipants));
        OnPropertyChanged(nameof(HasReturns));
        OnPropertyChanged(nameof(HasError));
    }

    private static string Describe(Exception exception) => exception is ApiException api
        ? ApiErrors.Describe(api)
        : Loc.Instance["err_no_connection"];
}

public sealed record SaleDetailItemRow(SaleDetailItemDto Item)
{
    public string Name => string.IsNullOrWhiteSpace(Item.VariantName)
        ? Item.ProductName
        : $"{Item.ProductName} · {Item.VariantName}";
    public string QuantityPrice => $"{Item.Quantity:0.###} {Item.UnitName} × {Item.UnitPrice:N0}";
    public string Total => Item.LineTotal.ToString("N0");
    public bool HasReturned => Item.ReturnedQuantity > 0;
    public string Returned => $"{Loc.Instance["returned"]}: {Item.ReturnedQuantity:0.###} {Item.UnitName}";
}

public sealed record SaleDetailPaymentRow(SaleDetailPaymentDto Payment)
{
    public string Method => Loc.Instance[$"pay_{Payment.Method.ToLowerInvariant()}"];
    public string Amount => $"{Payment.Amount:N2} {Payment.Currency}";
    public string BaseAmount => Payment.Rate == 1 ? "" : $"≈ {Payment.AmountBase:N0} UZS";
    public bool HasBaseAmount => Payment.Rate != 1;
}
