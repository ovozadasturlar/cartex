using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Sales;
using Cartex.UI.ViewModels;
using Cartex.UI.Views;

namespace Cartex.UI.Services;

/// One place that knows how to build and show the receipt dialog, so POS, sales history and the
/// customer profile all open the same thing instead of each repeating the wiring.
public sealed class ReceiptDialogService(
    IReceiptApi receiptApi,
    ISalesApi salesApi,
    AuthService auth,
    PrintDispatchService print,
    IDialogService dialog,
    IToastService toast,
    IBusyService busy)
{
    public Task<ReceiptDialogResult> ShowAsync(SaleDto sale) =>
        ShowAsync(sale.ReceiptToken, sale.Id);

    public async Task<ReceiptDialogResult> ShowAsync(string? receiptToken, long saleId)
    {
        var vm = new ReceiptDetailViewModel(receiptApi, salesApi, auth, print, dialog, toast, busy,
            receiptToken: receiptToken, saleId: saleId);
        _ = vm.InitAsync();
        return await dialog.ShowAsync<ReceiptDetailDialog, ReceiptDetailViewModel, ReceiptDialogResult>(vm);
    }
}
