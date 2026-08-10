using System.Text.Json;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Printing;
using Cartex.Shared.Models.Sales;
using Cartex.Shared.Models.Shifts;

namespace Cartex.UI.Services;

public sealed class PrintDispatchService(
    IPrintingApi printing,
    AuthService auth,
    BranchContextService branch,
    PrintStatusHubService statusHub,
    IToastService toast)
{
    private readonly AuthService _auth = auth;
    private readonly BranchContextService _branch = branch;

    public Task PrintReceiptAsync(ReceiptDto receipt, bool reprint, CancellationToken cancellationToken = default) =>
        CreateAsync(
            PrintJobKind.Receipt,
            "receipt_token",
            receipt.ReceiptToken,
            JsonSerializer.SerializeToElement(new { receiptToken = receipt.ReceiptToken }),
            reprint,
            reprint ? "manual_reprint" : null,
            reprint ? $"receipt-reprint:{receipt.ReceiptToken}:{Guid.NewGuid():N}" : $"receipt:{receipt.ReceiptToken}",
            cancellationToken);

    public Task PrintZReportAsync(ZReportDto report, bool reprint, CancellationToken cancellationToken = default) =>
        CreateAsync(
            PrintJobKind.ZReport,
            "shift",
            report.ShiftId.ToString(),
            JsonSerializer.SerializeToElement(new { shiftId = report.ShiftId }),
            reprint,
            reprint ? "manual_reprint" : null,
            reprint ? $"z-reprint:{report.ShiftId}:{Guid.NewGuid():N}" : $"z:{report.ShiftId}",
            cancellationToken);

    public Task PrintIssueNoteAsync(long issueId, long? branchId = null, CancellationToken cancellationToken = default) =>
        CreateAsync(
            PrintJobKind.Receipt,
            "goods_issue",
            issueId.ToString(),
            JsonSerializer.SerializeToElement(new { issueId }),
            false,
            null,
            $"issue-note:{issueId}:{Guid.NewGuid():N}",
            cancellationToken,
            branchId: branchId,
            kindLabel: LocalizationManager.Instance["print_kind_issue_note"]);

    public Task PrintBarcodeAsync(
        string code,
        string name,
        int copies,
        string? priceText,
        string? sku,
        bool withPrice,
        bool showSku,
        CancellationToken cancellationToken = default) =>
        CreateAsync(
            PrintJobKind.BarcodeLabel,
            "barcode",
            code,
            JsonSerializer.SerializeToElement(new { code, name, priceText, sku, withPrice, showSku }),
            false,
            null,
            $"barcode:{_auth.DeviceId}:{Guid.NewGuid():N}",
            cancellationToken,
            copies);

    private async Task CreateAsync(
        PrintJobKind kind,
        string sourceType,
        string sourceId,
        JsonElement payload,
        bool reprint,
        string? reason,
        string idempotencyKey,
        CancellationToken cancellationToken,
        int copies = 1,
        long? branchId = null,
        string? kindLabel = null)
    {
        var permission = kind switch
        {
            PrintJobKind.Receipt => reprint ? "printing.receipts.reprint" : "printing.receipts.print",
            PrintJobKind.BarcodeLabel => "printing.barcodes.print",
            PrintJobKind.ZReport => "printing.z_reports.print",
            _ => "printing.documents.print"
        };
        if (!_auth.HasPermission("printing.remote.use"))
            throw new UnauthorizedAccessException("Tarmoq orqali chop etish ruxsati kerak.");
        if (!_auth.HasPermission(permission))
            throw new UnauthorizedAccessException("Ushbu turdagi chop etish ruxsati kerak.");
        var targetBranchId = branchId ?? _branch.CurrentBranchId;
        if (targetBranchId is null)
            throw new InvalidOperationException("Chop etish uchun filial tanlanmagan.");

        await statusHub.EnsureStartedAsync();
        var job = await printing.CreateJobAsync(new CreatePrintJobRequest(
            targetBranchId.Value,
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
        var label = kindLabel ?? PrintNotificationText.Kind(kind);
        if (job.Status == PrintJobStatus.Completed)
            toast.Success(string.Format(LocalizationManager.Instance["print_completed"], label, string.Empty));
        else
            toast.Info(string.Format(LocalizationManager.Instance["print_queued"], label));
    }
}
