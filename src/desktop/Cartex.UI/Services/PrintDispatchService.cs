using System.Net.Sockets;
using System.Text.Json;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Printing;
using Cartex.Shared.Models.Sales;
using Cartex.Shared.Models.Settings;
using Cartex.Shared.Models.Shifts;

namespace Cartex.UI.Services;

public sealed class PrintDispatchService
{
    private readonly IPrintingApi _printing;
    private readonly IPrinterService _printer;
    private readonly IBarcodeLabelService _labels;
    private readonly OfflinePrintJournal _offlineJournal;
    private readonly PrintPolicyCache _policyCache;
    private readonly PrintLogoCache _logoCache;
    private readonly PrintStatusHubService _statusHub;
    private readonly IToastService _toast;
    private readonly AuthService _auth;
    private readonly BranchContextService _branch;
    private readonly SemaphoreSlim _flushLock = new(1, 1);
    private readonly Lock _localRateLock = new();
    private readonly Queue<(DateTime At, int Copies)> _localPrints = new();

    public PrintDispatchService(
        IPrintingApi printing,
        IPrinterService printer,
        IBarcodeLabelService labels,
        OfflinePrintJournal offlineJournal,
        PrintPolicyCache policyCache,
        PrintLogoCache logoCache,
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
        _policyCache = policyCache;
        _logoCache = logoCache;
        _statusHub = statusHub;
        _toast = toast;
        _auth = auth;
        _branch = branch;
        connectivity.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(ConnectivityService.IsOnline)
                && connectivity.IsOnline && _auth.IsAuthenticated)
                _ = FlushOfflineSafeAsync();
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
            copies: Math.Clamp(_printer.GetSettings().ReceiptCopies, 1, 100),
            printLocally: () => PrintReceiptLocallyAsync(receipt, cancellationToken));

    /// The job payload needs only the token, so checkout auto-print is dispatched without
    /// waiting for the receipt fetch; the fetch is awaited only for the local fallback.
    public async Task PrintReceiptAsync(string receiptToken, Task<ReceiptDto> receiptFetch, CancellationToken cancellationToken = default)
    {
        await CreateAsync(
            PrintJobKind.Receipt,
            "receipt_token",
            receiptToken,
            JsonSerializer.SerializeToElement(new { receiptToken }),
            false,
            null,
            $"receipt:{receiptToken}",
            cancellationToken,
            copies: Math.Clamp(_printer.GetSettings().ReceiptCopies, 1, 100),
            printLocally: async () =>
                await PrintReceiptLocallyAsync(await receiptFetch, cancellationToken));
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
            printLocally: () => RunLocalAsync(() => _printer.PrintZReport(report)));

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
            printLocally: document is null
                ? null
                : () => RunLocalAsync(() => _printer.PrintProforma(document, cartCode)));

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
            printLocally: () => RunLocalAsync(() =>
            {
                var target = _printer.GetSettings().BarcodePrinter;
                if (string.IsNullOrWhiteSpace(target))
                    throw new InvalidOperationException(LocalizationManager.Instance["printer_not_set"]);
                _labels.PrintLabels(code, name, copies, target, withPrice ? priceText : null, sku);
            }));

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
        Func<Task>? printLocally = null)
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

        // RUXSAT-04: modul o'chiq bo'lsa server konveyeri yopiq — to'liq huquqli (wildcard)
        // foydalanuvchida ham ruxsat tekshiruvi o'tib ketadi, shuning uchun modul holati
        // alohida so'raladi. Hujjat to'g'ridan-to'g'ri shu kompyuterning printerida chiqadi.
        if (!_auth.HasPermission("printing.remote.use")
            || !SettingsService.Instance.IsFeatureOn("remote_printing"))
        {
            if (printLocally is null)
                throw new UnauthorizedAccessException("Tarmoq orqali chop etish ruxsati kerak.");
            await ExecuteLocalAsync(printLocally, label);
            return;
        }
        var targetBranchId = branchId ?? _branch.CurrentBranchId;
        if (targetBranchId is null)
            throw new InvalidOperationException("Chop etish uchun filial tanlanmagan.");
        var localPolicy = await _policyCache.GetAsync(targetBranchId.Value, kind, cancellationToken);
        var salesPolicyAllows = localPolicy is not null && SalesPolicyAllows(localPolicy.Sales, kind, sourceType);
        if (LocalPrintRouting.ShouldPrintLocally(
                localPolicy?.Routing,
                printLocally is not null,
                localPolicy?.HasEnabledLocalEndpoint == true && HasLocalPrinter(kind),
                salesPolicyAllows))
        {
            EnsureLocalRate(localPolicy!.Routing, copies);
            await ExecuteLocalAsync(printLocally!, label);
            await AddToJournalAsync(
                targetBranchId.Value, kind, sourceType, sourceId, payload, copies, reprint, reason, idempotencyKey);
            _ = FlushOfflineSafeAsync();
            return;
        }

        _ = FlushOfflineSafeAsync();
        await _statusHub.EnsureStartedAsync();
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
                    return;
                }
            }
        }
        finally
        {
            _flushLock.Release();
        }
    }

    private async Task FlushOfflineSafeAsync()
    {
        try
        {
            await TryFlushOfflineAsync();
        }
        catch
        {
            return;
        }
    }

    private async Task AddToJournalAsync(
        long branchId,
        PrintJobKind kind,
        string sourceType,
        string sourceId,
        JsonElement payload,
        int copies,
        bool reprint,
        string? reason,
        string idempotencyKey) =>
        await _offlineJournal.AddAsync(new OfflinePrintRecord(
            branchId,
            kind,
            sourceType,
            sourceId,
            payload.GetRawText(),
            copies,
            reprint,
            reason,
            idempotencyKey,
            DateTime.UtcNow));

    private async Task ExecuteLocalAsync(Func<Task> printLocally, string label, bool showSuccess = true)
    {
        try
        {
            await printLocally();
        }
        catch (Exception exception)
        {
            _toast.Error(ApiErrors.Describe(exception));
            throw;
        }

        if (showSuccess)
            _toast.Success(string.Format(LocalizationManager.Instance["print_completed"], label, string.Empty));
    }

    private async Task PrintReceiptLocallyAsync(ReceiptDto receipt, CancellationToken cancellationToken)
    {
        var target = _printer.ReceiptTarget();
        if (target.IsDocument)
            throw new InvalidOperationException(LocalizationManager.Instance["print_offline_needs_thermal"]);
        if (string.IsNullOrWhiteSpace(target.Printer))
            throw new InvalidOperationException(LocalizationManager.Instance["printer_not_set"]);

        var options = _printer.ReceiptOptions;
        var imageKey = !string.IsNullOrWhiteSpace(receipt.MonochromeLogoImageKey)
            ? receipt.MonochromeLogoImageKey
            : receipt.LogoImageKey;
        if (options?.ShowLogo == true && !string.IsNullOrWhiteSpace(imageKey))
        {
            var width = _printer.ReceiptRasterWidth(target.Printer, options.Width);
            var raster = await _logoCache.GetForPrintAsync(imageKey, width, cancellationToken);
            if (raster is null)
                _toast.Warning(LocalizationManager.Instance["print_logo_unavailable"]);
            else
                options = options with { LogoRasterBytes = raster };
        }

        _printer.PrintReceipt(receipt, target.Printer, _printer.GetSettings().ReceiptCopies, options);
    }

    private bool HasLocalPrinter(PrintJobKind kind)
    {
        var settings = _printer.GetSettings();
        var printer = kind switch
        {
            PrintJobKind.Receipt => _printer.ReceiptTarget().Printer,
            PrintJobKind.BarcodeLabel => settings.BarcodePrinter,
            PrintJobKind.ZReport => _printer.ZReportTarget().Printer,
            PrintJobKind.Document => settings.DocumentPrinter,
            PrintJobKind.CartProforma => _printer.ProformaTarget(ProformaPrintOptions.Resolve(settings)).Printer,
            _ => null
        };
        return !string.IsNullOrWhiteSpace(printer);
    }

    private static bool SalesPolicyAllows(SalesPolicyDto sales, PrintJobKind kind, string sourceType) =>
        sourceType switch
        {
            "customer_payment" or "customer_refund" => sales.PrintMoneyDocuments,
            _ when kind == PrintJobKind.CartProforma => sales.PrintCartProforma,
            _ => true
        };

    private void EnsureLocalRate(PrintRoutingPolicyDto policy, int copies)
    {
        var now = DateTime.UtcNow;
        lock (_localRateLock)
        {
            while (_localPrints.TryPeek(out var print) && now - print.At >= TimeSpan.FromMinutes(1))
                _localPrints.Dequeue();
            if (_localPrints.Sum(x => x.Copies) + copies > policy.MaxCopiesPerMinute)
            {
                _toast.Error(LocalizationManager.Instance["print_rate_limit_exceeded"]);
                throw new InvalidOperationException(LocalizationManager.Instance["print_rate_limit_exceeded"]);
            }
            _localPrints.Enqueue((now, copies));
        }
    }

    private static Task RunLocalAsync(Action action)
    {
        action();
        return Task.CompletedTask;
    }

    public static bool IsServerUnavailable(Exception exception) => exception switch
    {
        Refit.ApiException api => (int)api.StatusCode >= 500,
        HttpRequestException or SocketException or TimeoutException => true,
        TaskCanceledException => true,
        _ => exception.InnerException is not null && IsServerUnavailable(exception.InnerException)
    };
}
