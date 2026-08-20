using System.Net.Sockets;
using System.Text.Json;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Printing;
using Cartex.Shared.Models.Sales;
using Cartex.Shared.Models.Shifts;

namespace Cartex.UI.Services;

public sealed class PrintDispatchService
{
    private readonly IPrintingApi _printing;
    private readonly IPrinterService _printer;
    private readonly IBarcodeLabelService _labels;
    private readonly OfflinePrintJournal _offlineJournal;
    private readonly PrintStatusHubService _statusHub;
    private readonly IToastService _toast;
    private readonly AuthService _auth;
    private readonly BranchContextService _branch;
    private readonly SemaphoreSlim _flushLock = new(1, 1);

    public PrintDispatchService(
        IPrintingApi printing,
        IPrinterService printer,
        IBarcodeLabelService labels,
        OfflinePrintJournal offlineJournal,
        AuthService auth,
        BranchContextService branch,
        PrintStatusHubService statusHub,
        ConnectivityService connectivity,
        IToastService toast)
    {
        _printing = printing;
        _printer = printer;
        _labels = labels;
        _offlineJournal = offlineJournal;
        _statusHub = statusHub;
        _toast = toast;
        _auth = auth;
        _branch = branch;
        connectivity.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(ConnectivityService.IsOnline)
                && connectivity.IsOnline && _auth.IsAuthenticated)
                _ = TryFlushOfflineAsync();
        };
    }

    public Task PrintReceiptAsync(ReceiptDto receipt, bool reprint, CancellationToken cancellationToken = default) =>
        CreateAsync(
            PrintJobKind.Receipt,
            "receipt_token",
            receipt.ReceiptToken,
            JsonSerializer.SerializeToElement(new { receiptToken = receipt.ReceiptToken }),
            reprint,
            reprint ? "manual_reprint" : null,
            reprint ? $"receipt-reprint:{receipt.ReceiptToken}:{Guid.NewGuid():N}" : $"receipt:{receipt.ReceiptToken}",
            cancellationToken,
            printLocally: () =>
            {
                if (_printer.ReceiptTarget().IsDocument)
                    throw new InvalidOperationException(LocalizationManager.Instance["print_offline_needs_thermal"]);
                _printer.PrintReceipt(receipt);
            });

    /// The job payload needs only the token, so checkout auto-print is dispatched without
    /// waiting for the receipt fetch; the fetch is awaited only for the local fallback.
    public async Task PrintReceiptAsync(string receiptToken, Task<ReceiptDto> receiptFetch, CancellationToken cancellationToken = default)
    {
        try
        {
            await CreateAsync(
                PrintJobKind.Receipt,
                "receipt_token",
                receiptToken,
                JsonSerializer.SerializeToElement(new { receiptToken }),
                false,
                null,
                $"receipt:{receiptToken}",
                cancellationToken);
        }
        catch (Exception exception) when (IsServerUnavailable(exception))
        {
            await PrintReceiptAsync(await receiptFetch, false, cancellationToken);
        }
    }

    public Task PrintZReportAsync(ZReportDto report, bool reprint, CancellationToken cancellationToken = default) =>
        CreateAsync(
            PrintJobKind.ZReport,
            "shift",
            report.ShiftId.ToString(),
            JsonSerializer.SerializeToElement(new { shiftId = report.ShiftId }),
            reprint,
            reprint ? "manual_reprint" : null,
            reprint ? $"z-reprint:{report.ShiftId}:{Guid.NewGuid():N}" : $"z:{report.ShiftId}",
            cancellationToken,
            printLocally: () => _printer.PrintZReport(report));

    public Task PrintReturnAsync(long returnId, long? branchId = null, CancellationToken cancellationToken = default) =>
        CreateAsync(
            PrintJobKind.Receipt,
            "customer_return",
            returnId.ToString(),
            JsonSerializer.SerializeToElement(new { returnId }),
            false,
            null,
            $"return:{returnId}:{Guid.NewGuid():N}",
            cancellationToken,
            branchId: branchId,
            kindLabel: LocalizationManager.Instance["print_kind_return"]);

    /// HUJJ-04: money moving without paper is the one gap a customer actually feels — they hand
    /// over cash and walk out with nothing. Payment and payout slips go through the same routing
    /// and permission path as every other document.
    public Task PrintCustomerPaymentAsync(long paymentId, long? branchId = null, CancellationToken cancellationToken = default) =>
        CreateAsync(
            PrintJobKind.Receipt,
            "customer_payment",
            paymentId.ToString(),
            JsonSerializer.SerializeToElement(new { paymentId }),
            false,
            null,
            $"payment:{paymentId}:{Guid.NewGuid():N}",
            cancellationToken,
            branchId: branchId,
            kindLabel: LocalizationManager.Instance["print_kind_payment"]);

    public Task PrintCustomerRefundAsync(long refundId, long? branchId = null, CancellationToken cancellationToken = default) =>
        CreateAsync(
            PrintJobKind.Receipt,
            "customer_refund",
            refundId.ToString(),
            JsonSerializer.SerializeToElement(new { refundId }),
            false,
            null,
            $"refund:{refundId}:{Guid.NewGuid():N}",
            cancellationToken,
            branchId: branchId,
            kindLabel: LocalizationManager.Instance["print_kind_payout"]);

    /// The proforma references the saved cart rather than carrying its contents, so the server
    /// stays in control of what can be printed and the routing policy still applies. The local
    /// copy of the document lets the same content print on this machine when the server is away.
    public Task PrintCartProformaAsync(string cartCode, PreviewDocument? document = null, CancellationToken cancellationToken = default) =>
        CreateAsync(
            PrintJobKind.CartProforma,
            "cart",
            cartCode,
            JsonSerializer.SerializeToElement(new { cartCode }),
            false,
            null,
            $"proforma:{cartCode}:{Guid.NewGuid():N}",
            cancellationToken,
            kindLabel: LocalizationManager.Instance["print_kind_preview"],
            printLocally: document is null ? null : () => _printer.PrintProforma(document, cartCode));

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
            copies,
            printLocally: () =>
            {
                var target = _printer.GetSettings().BarcodePrinter;
                if (string.IsNullOrWhiteSpace(target))
                    throw new InvalidOperationException(LocalizationManager.Instance["printer_not_set"]);
                _labels.PrintLabels(code, name, copies, target, withPrice ? priceText : null, sku);
            });

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
        string? kindLabel = null,
        Action? printLocally = null)
    {
        var permission = kind switch
        {
            PrintJobKind.Receipt => reprint ? "printing.receipts.reprint" : "printing.receipts.print",
            PrintJobKind.BarcodeLabel => "printing.barcodes.print",
            PrintJobKind.ZReport => "printing.z_reports.print",
            _ => "printing.documents.print"
        };
        if (!_auth.HasPermission(permission))
            throw new UnauthorizedAccessException("Ushbu turdagi chop etish ruxsati kerak.");
        var label = kindLabel ?? PrintNotificationText.Kind(kind);

        // Tarmoq chop etish imkoniyati/ruxsati bo'lmagan do'konda server konveyeri ishlamaydi —
        // hujjat to'g'ridan-to'g'ri shu kompyuterning printerida chiqadi.
        if (!_auth.HasPermission("printing.remote.use"))
        {
            if (printLocally is null)
                throw new UnauthorizedAccessException("Tarmoq orqali chop etish ruxsati kerak.");
            printLocally();
            _toast.Success(string.Format(LocalizationManager.Instance["print_completed"], label, string.Empty));
            return;
        }
        var targetBranchId = branchId ?? _branch.CurrentBranchId;
        if (targetBranchId is null)
            throw new InvalidOperationException("Chop etish uchun filial tanlanmagan.");
        _ = TryFlushOfflineAsync();
        await _statusHub.EnsureStartedAsync();
        try
        {
            var job = await _printing.CreateJobAsync(new CreatePrintJobRequest(
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
            if (job.Status == PrintJobStatus.Completed)
                _toast.Success(string.Format(LocalizationManager.Instance["print_completed"], label, string.Empty));
            else if (job.Status == PrintJobStatus.Pending && job.AssignedNodeId is null)
                _toast.Warning(string.Format(LocalizationManager.Instance["print_no_online_printer"], label));
            else
                _toast.Info(string.Format(LocalizationManager.Instance["print_queued"], label));
        }
        catch (Exception exception) when (printLocally is not null
            && !cancellationToken.IsCancellationRequested && IsServerUnavailable(exception))
        {
            printLocally();
            await _offlineJournal.AddAsync(new OfflinePrintRecord(
                targetBranchId.Value, kind, sourceType, sourceId, payload.GetRawText(),
                copies, reprint, reason, idempotencyKey, DateTime.UtcNow));
            _toast.Warning(string.Format(LocalizationManager.Instance["print_offline_local"], label));
        }
    }

    /// Records left behind by offline printing are pushed to the server as completed
    /// history. The idempotency key is the one from the original attempt, so a request
    /// that actually reached the server never becomes a duplicate job.
    public async Task TryFlushOfflineAsync()
    {
        if (!_flushLock.Wait(0)) return;
        try
        {
            foreach (var record in await _offlineJournal.GetAllAsync())
            {
                try
                {
                    using var payload = JsonDocument.Parse(record.PayloadJson);
                    await _printing.CreateJobAsync(new CreatePrintJobRequest(
                        record.BranchId,
                        record.Kind,
                        record.SourceType,
                        record.SourceId,
                        payload.RootElement.Clone(),
                        record.Copies,
                        record.IsReprint,
                        record.Reason,
                        record.IdempotencyKey,
                        _auth.DeviceId,
                        _auth.DeviceName,
                        CompletedLocally: true));
                    await _offlineJournal.RemoveAsync(record.IdempotencyKey);
                }
                catch (Exception exception) when (IsServerUnavailable(exception))
                {
                    return;
                }
                catch
                {
                    await _offlineJournal.RemoveAsync(record.IdempotencyKey);
                }
            }
        }
        finally
        {
            _flushLock.Release();
        }
    }

    public static bool IsServerUnavailable(Exception exception) => exception switch
    {
        Refit.ApiException api => (int)api.StatusCode >= 500,
        HttpRequestException or SocketException or TimeoutException => true,
        TaskCanceledException => true,
        _ => exception.InnerException is not null && IsServerUnavailable(exception.InnerException)
    };
}
