using System.Text.Json;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Printing;
using Cartex.Shared.Models.Sales;
using Cartex.Shared.Models.Shifts;

namespace Cartex.UI.Services;

public sealed class PrintDispatchService(
    IPrintingApi printing,
    AuthService auth,
    BranchContextService branch)
{
    private readonly AuthService _auth = auth;
    private readonly BranchContextService _branch = branch;

    public Task<bool> TryReceiptAsync(ReceiptDto receipt, bool reprint, CancellationToken cancellationToken = default) =>
        TryCreateAsync(
            PrintJobKind.Receipt,
            "receipt_token",
            receipt.ReceiptToken,
            JsonSerializer.SerializeToElement(new { receiptToken = receipt.ReceiptToken }),
            reprint,
            reprint ? "manual_reprint" : null,
            reprint ? $"receipt-reprint:{receipt.ReceiptToken}:{Guid.NewGuid():N}" : $"receipt:{receipt.ReceiptToken}",
            cancellationToken);

    public Task<bool> TryZReportAsync(ZReportDto report, bool reprint, CancellationToken cancellationToken = default) =>
        TryCreateAsync(
            PrintJobKind.ZReport,
            "shift",
            report.ShiftId.ToString(),
            JsonSerializer.SerializeToElement(new { shiftId = report.ShiftId }),
            reprint,
            reprint ? "manual_reprint" : null,
            reprint ? $"z-reprint:{report.ShiftId}:{Guid.NewGuid():N}" : $"z:{report.ShiftId}",
            cancellationToken);

    public Task<bool> TryBarcodeAsync(
        string code,
        string name,
        int copies,
        string? priceText,
        string? sku,
        bool withPrice,
        bool showSku,
        CancellationToken cancellationToken = default) =>
        TryCreateAsync(
            PrintJobKind.BarcodeLabel,
            "barcode",
            code,
            JsonSerializer.SerializeToElement(new { code, name, priceText, sku, withPrice, showSku }),
            false,
            null,
            $"barcode:{_auth.DeviceId}:{Guid.NewGuid():N}",
            cancellationToken,
            copies);

    private async Task<bool> TryCreateAsync(
        PrintJobKind kind,
        string sourceType,
        string sourceId,
        JsonElement payload,
        bool reprint,
        string? reason,
        string idempotencyKey,
        CancellationToken cancellationToken,
        int copies = 1)
    {
        var permission = kind switch
        {
            PrintJobKind.Receipt => reprint ? "printing.receipts.reprint" : "printing.receipts.print",
            PrintJobKind.BarcodeLabel => "printing.barcodes.print",
            PrintJobKind.ZReport => "printing.z_reports.print",
            _ => "printing.documents.print"
        };
        if (!_auth.HasPermission("printing.remote.use") || !_auth.HasPermission(permission)
            || _branch.CurrentBranchId is not long branchId)
            return false;
        try
        {
            await printing.CreateJobAsync(new CreatePrintJobRequest(
                branchId,
                kind,
                sourceType,
                sourceId,
                payload,
                copies,
                reprint,
                reason,
                idempotencyKey,
                _auth.DeviceId,
                _auth.DeviceName), cancellationToken);
            return true;
        }
        catch (Refit.ApiException exception) when ((int)exception.StatusCode is 403 or 404)
        {
            return false;
        }
    }
}
