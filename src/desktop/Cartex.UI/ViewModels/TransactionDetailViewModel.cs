using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Irihi.Avalonia.Shared.Contracts;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Customers;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public enum TransactionDialogResult { None, Voided, SaleOpened }

/// One dialog for a single ledger movement, opened from both the customer ledger and the
/// statement timeline so the two pages behave the same.
public partial class TransactionDetailViewModel : ViewModelBase, IDialogContext
{
    private readonly ICustomerPaymentsApi _paymentsApi;
    private readonly ReceiptDialogService _receiptDialog;
    private readonly IDialogService _dialog;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private readonly AuthService _auth;
    private readonly PrintDispatchService _print;

    public TransactionDetailViewModel(
        ICustomerPaymentsApi paymentsApi,
        ReceiptDialogService receiptDialog,
        IDialogService dialog,
        IToastService toast,
        IBusyService busy,
        AuthService auth,
        PrintDispatchService print,
        DateTime occurredAt,
        string operation,
        string? documentNumber,
        decimal debit,
        decimal credit,
        decimal balanceAfter,
        string? currency,
        long? saleId,
        long? paymentDocumentId)
    {
        _paymentsApi = paymentsApi;
        _receiptDialog = receiptDialog;
        _dialog = dialog;
        _toast = toast;
        _busy = busy;
        _auth = auth;
        _print = print;
        OccurredAt = occurredAt;
        Operation = operation;
        DocumentNumber = documentNumber;
        Debit = debit;
        Credit = credit;
        BalanceAfter = balanceAfter;
        Currency = currency;
        SaleId = saleId;
        PaymentDocumentId = paymentDocumentId;
    }

    public DateTime OccurredAt { get; }
    public string Operation { get; }
    public string? DocumentNumber { get; }
    public decimal Debit { get; }
    public decimal Credit { get; }
    public decimal BalanceAfter { get; }
    public string? Currency { get; }
    public long? SaleId { get; }
    public long? PaymentDocumentId { get; }

    public bool HasDocumentNumber => !string.IsNullOrWhiteSpace(DocumentNumber);
    public bool CanOpenSale => SaleId is > 0 && _auth.HasPermission("sales.view");
    public bool CanVoidPayment => PaymentDocumentId is > 0 && _auth.HasPermission("customer_payments.void");
    public bool CanPrintPayment => PaymentDocumentId is > 0 && _auth.HasPermission("printing.receipts.print");
    public bool HasActions => CanOpenSale || CanVoidPayment || CanPrintPayment;
    public string AmountText => Debit > 0 ? $"+{Debit:N0}" : $"-{Credit:N0}";
    public bool IsIncrease => Debit > 0;

    [RelayCommand]
    private async Task OpenSaleAsync()
    {
        if (SaleId is not { } saleId || !CanOpenSale) return;
        RequestClose?.Invoke(this, TransactionDialogResult.SaleOpened);
        await _receiptDialog.ShowAsync(null, saleId);
    }

    [RelayCommand]
    private async Task PrintPaymentAsync()
    {
        if (PaymentDocumentId is not { } paymentId || !CanPrintPayment) return;
        try
        {
            await _print.PrintCustomerPaymentAsync(paymentId);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task VoidPaymentAsync()
    {
        if (PaymentDocumentId is not { } paymentId || !CanVoidPayment) return;
        var details = string.Format(L["void_payment_confirm"],
            DocumentNumber ?? $"#{paymentId}", Math.Max(Debit, Credit).ToString("N0"), OccurredAt);
        var reason = await _dialog.PromptAsync(L["void_payment"], details, L["reason"]);
        if (string.IsNullOrWhiteSpace(reason)) return;
        try
        {
            using (_busy.Begin(L["loading"]))
                await _paymentsApi.VoidAsync(paymentId, new VoidCustomerPaymentRequest(reason.Trim()));
            _toast.Success(L["success"]);
            RequestClose?.Invoke(this, TransactionDialogResult.Voided);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    public event EventHandler<object?>? RequestClose;

    [RelayCommand]
    private void Close() => RequestClose?.Invoke(this, TransactionDialogResult.None);

    void IDialogContext.Close() => RequestClose?.Invoke(this, TransactionDialogResult.None);
}
