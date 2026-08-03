using System.Text.Json;
using Cartex.ApiClient.Api;
using Cartex.Mobile.Core;
using Cartex.Shared.Models.Printing;
using Cartex.Shared.Models.Sales;

namespace Cartex.Mobile.Store.Services;

public sealed class MobilePrintDispatcher(
    IPrintingApi printingApi,
    MobileAuthService auth,
    MobilePermissions permissions)
{
    public bool CanPrintBarcode => Allowed("printing.barcodes.print");
    public bool CanPrintReceipt => Allowed("printing.receipts.print");
    public bool CanReprintReceipt => Allowed("printing.receipts.reprint");
    public bool CanPrintZReport => Allowed("printing.z_reports.print");

    public Task PrintBarcodeAsync(string code, string name, int copies, string? priceText, string? sku, bool withPrice) =>
        CreateAsync(
            PrintJobKind.BarcodeLabel,
            "barcode",
            code,
            new { code, name, priceText, sku, withPrice },
            Math.Clamp(copies, 1, 500),
            false,
            null,
            $"barcode:{code}:mobile:{Guid.NewGuid():N}");

    public Task ReprintReceiptAsync(SaleDto sale) =>
        CreateAsync(
            PrintJobKind.Receipt,
            "sale",
            sale.Id.ToString(),
            new { receiptToken = sale.ReceiptToken },
            1,
            true,
            "mobile_reprint",
            $"receipt-reprint:{sale.Id}:mobile:{Guid.NewGuid():N}");

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

    private bool Allowed(string permission) =>
        permissions.Has("printing.remote.use") && permissions.Has(permission);

    private Task CreateAsync(
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

        return printingApi.CreateJobAsync(new CreatePrintJobRequest(
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
    }
}
