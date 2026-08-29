using System.Text.Json;
using Cartex.ApiClient.Api;
using Cartex.Mobile.Core;
using Cartex.Shared.Models.Printing;
using Cartex.Shared.Models.Sales;

namespace Cartex.Mobile.Store.Services;

public sealed class MobilePrintDispatcher(
    IPrintingApi printingApi,
    MobileAuthService auth,
    AccessState access)
{
    public bool CanPrintBarcode => access.CanPrintBarcode;
    public bool CanPrintReceipt => access.CanPrintReceipt;
    public bool CanReprintReceipt => access.CanReprintReceipt;
    public bool CanPrintZReport => access.CanPrintZReport;

    public Task PrintBarcodeAsync(string code, string name, int copies, string? priceText, string? sku, bool withPrice, bool showSku) =>
        CreateAsync(
            PrintJobKind.BarcodeLabel,
            "barcode",
            code,
            new { code, name, priceText, sku, withPrice, showSku },
            Math.Clamp(copies, 1, 500),
            false,
            null,
            $"barcode:{code}:mobile:{Guid.NewGuid():N}");

    public Task ReprintReceiptAsync(SaleDto sale) =>
        ReprintReceiptAsync(sale.Id, sale.ReceiptToken);

    public Task ReprintReceiptAsync(SaleListDto sale) =>
        ReprintReceiptAsync(sale.Id, sale.ReceiptToken);

    public Task ReprintReceiptAsync(long saleId, string receiptToken) =>
        CreateAsync(
            PrintJobKind.Receipt,
            "sale",
            saleId.ToString(),
            new { receiptToken },
            1,
            true,
            "mobile_reprint",
            $"receipt-reprint:{saleId}:mobile:{Guid.NewGuid():N}");

    public Task PrintZReportAsync(long shiftId) =>
        CreateAsync(
            PrintJobKind.ZReport,
            "shift",
            shiftId.ToString(),
            new { shiftId },
            1,
            true,
            "mobile_reprint",
            $"z-report:{shiftId}:mobile:{Guid.NewGuid():N}");

    public Task PrintIssueNoteAsync(long issueId) =>
        CreateAsync(
            PrintJobKind.Receipt,
            "goods_issue",
            issueId.ToString(),
            new { issueId },
            1,
            false,
            null,
            $"issue-note:{issueId}:mobile:{Guid.NewGuid():N}");

    private async Task CreateAsync(
        PrintJobKind kind,
        string sourceType,
        string sourceId,
        object payload,
        int copies,
        bool isReprint,
        string? reason,
        string idempotencyKey)
    {
        if (auth.DefaultBranchId is not long branchId)
            throw new InvalidOperationException(Loc.Instance["warehouse_none"]);

        var job = await printingApi.CreateJobAsync(new CreatePrintJobRequest(
            branchId,
            kind,
            sourceType,
            sourceId,
            JsonSerializer.SerializeToElement(payload),
            copies,
            isReprint,
            reason,
            idempotencyKey,
            auth.DeviceId,
            auth.DeviceName));

        // Server hech bir hostga tayinlay olmagan bo'lsa telefon "yuborildi" deb aldamaydi.
        if (job.Status == PrintJobStatus.Pending && job.AssignedNodeId is null)
            throw new InvalidOperationException(Loc.Instance["print_no_online_printer"]);
        if (job.Status == PrintJobStatus.Rejected)
            throw new InvalidOperationException(job.ErrorMessage ?? Loc.Instance["print_no_online_printer"]);
    }
}
