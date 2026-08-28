using System.ComponentModel;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Business;
using Cartex.Shared.Models.Printing;
using Cartex.Shared.Models.Settings;
using Microsoft.AspNetCore.SignalR.Client;

namespace Cartex.UI.Services;

public sealed class PrintHostService
{
    private readonly IPrintingApi _printingApi;
    private readonly IReceiptApi _receiptApi;
    private readonly IShiftsApi _shiftsApi;
    private readonly ICustomerReturnsApi _returnsApi;
    private readonly ICustomerPaymentsApi _paymentsApi;
    private readonly ICustomerRefundsApi _refundsApi;
    private readonly IBusinessApi _businessApi;
    private readonly IOrderingApi _orderingApi;
    private readonly IPrinterService _printer;
    private readonly IBarcodeLabelService _labels;
    private readonly AuthService _auth;
    private readonly BranchContextService _branch;
    private readonly PrintHostJournal _journal;
    private readonly PrintHostCredentialStore _credentialStore;
    private readonly IFilePickerService _filePicker;
    private readonly PrintLogoCache _logoCache;
    private readonly PrintPolicyCache _policyCache;
    private readonly IToastService _toast;
    private readonly SemaphoreSlim _startLock = new(1, 1);
    private readonly SemaphoreSlim _processLock = new(1, 1);
    private readonly Lock _endpointCacheLock = new();
    private CancellationTokenSource? _lifetime;
    private HubConnection? _connection;
    private long? _registeredBranchId;
    private string? _hostToken;
    private string? _reportedFailure;
    private readonly HubSubscription _subscription = new();
    private DateTime _lastHeartbeatAt;
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(60);
    private int _processPending;
    private IReadOnlyList<PrinterEndpointRegistration> _cachedEndpoints = [];
    private DateTime _endpointsLoadedAt;
    private bool _endpointsInvalidated = true;
    private int _endpointPolicyRefreshPending = 1;

    /// The host used to retry a rejected registration forever without saying anything, so
    /// a till looked healthy while nothing could ever print. The reason is surfaced once.
    public string? LastFailure { get; private set; }
    public event Action? StatusChanged;

    private void ReportHostFailure(Exception exception)
    {
        var reason = ApiErrors.Describe(exception);
        LastFailure = reason;
        if (_reportedFailure == reason) return;
        _reportedFailure = reason;
        StatusChanged?.Invoke();
    }

    private void ClearHostFailure()
    {
        if (LastFailure is null && _reportedFailure is null) return;
        LastFailure = null;
        _reportedFailure = null;
        StatusChanged?.Invoke();
    }

    public PrintHostService(
        IPrintingApi printingApi,
        IReceiptApi receiptApi,
        IShiftsApi shiftsApi,
        ICustomerReturnsApi returnsApi,
        ICustomerPaymentsApi paymentsApi,
        ICustomerRefundsApi refundsApi,
        IBusinessApi businessApi,
        IOrderingApi orderingApi,
        IPrinterService printer,
        IBarcodeLabelService labels,
        AuthService auth,
        BranchContextService branch,
        PrintHostJournal journal,
        PrintHostCredentialStore credentialStore,
        IFilePickerService filePicker,
        PrintLogoCache logoCache,
        PrintPolicyCache policyCache,
        IToastService toast)
    {
        _printingApi = printingApi;
        _receiptApi = receiptApi;
        _shiftsApi = shiftsApi;
        _returnsApi = returnsApi;
        _paymentsApi = paymentsApi;
        _refundsApi = refundsApi;
        _businessApi = businessApi;
        _orderingApi = orderingApi;
        _printer = printer;
        _labels = labels;
        _auth = auth;
        _branch = branch;
        _journal = journal;
        _credentialStore = credentialStore;
        _filePicker = filePicker;
        _logoCache = logoCache;
        _policyCache = policyCache;
        _toast = toast;
        _hostToken = credentialStore.Load();
        _auth.LoggedOut += () => _ = StopAsync();
        _branch.PropertyChanged += BranchChanged;
        _printer.SettingsChanged += InvalidateEndpointCache;
    }

    public async Task StartAsync()
    {
        if (!_auth.HasPermission("printing.host")) return;
        await _startLock.WaitAsync();
        try
        {
            if (_lifetime is not null) return;
            _lifetime = new CancellationTokenSource();
            _ = RunAsync(_lifetime.Token);
        }
        finally
        {
            _startLock.Release();
        }
    }

    // Ish topshirig'i hub orqali keladi, so'rab olinmaydi: sikl faqat ro'yxatdan o'tishni va
    // hub obunasini tirik saqlaydi. Obuna yangilanganda kutayotgan ishlar darhol olinadi.
    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var backoff = TimeSpan.FromSeconds(1);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!_auth.IsAuthenticated
                    || !_auth.HasPermission("printing.host")
                    || !SettingsService.Instance.IsFeatureOn("remote_printing")
                    || _branch.CurrentBranchId is null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                    continue;
                }

                await RegisterAsync(_branch.CurrentBranchId.Value, cancellationToken);
                if (await EnsureHubAsync(cancellationToken))
                    await ProcessAssignedAsync(cancellationToken);

                // A full healthy cycle means whatever was shown in the settings banner
                // (e.g. rejected while the device was still untrusted) is over.
                ClearHostFailure();
                backoff = TimeSpan.FromSeconds(1);
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                ReportHostFailure(exception);
                try
                {
                    await Task.Delay(backoff, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                backoff = TimeSpan.FromSeconds(Math.Min(backoff.TotalSeconds * 2, 30));
            }
        }
    }

    private async Task RegisterAsync(long branchId, CancellationToken cancellationToken)
    {
        if (_registeredBranchId == branchId && DateTime.UtcNow - _lastHeartbeatAt < HeartbeatInterval) return;
        var endpoints = BuildEndpoints();
        if (_registeredBranchId != branchId)
        {
            // Re-read per attempt: the credential belongs to the server the app points at,
            // which can change between runs.
            _hostToken = _credentialStore.Load();
            var result = await _printingApi.RegisterNodeAsync(new RegisterPrintNodeRequest(
                _auth.DeviceId,
                _auth.DeviceName,
                branchId,
                typeof(PrintHostService).Assembly.GetName().Version?.ToString(),
                true,
                endpoints,
                _hostToken), cancellationToken);
            if (!string.IsNullOrWhiteSpace(result.HostToken))
            {
                _hostToken = result.HostToken;
                _credentialStore.Save(result.HostToken);
            }

            _registeredBranchId = branchId;
            _lastHeartbeatAt = DateTime.UtcNow;
            SchedulePolicyRefresh();
            ClearHostFailure();
            return;
        }

        try
        {
            await _printingApi.HeartbeatAsync(new PrintNodeHeartbeatRequest(
                _auth.DeviceId,
                endpoints,
                _hostToken ?? throw new InvalidOperationException("Print host credential is missing.")), cancellationToken);
            _lastHeartbeatAt = DateTime.UtcNow;
            if (Interlocked.Exchange(ref _endpointPolicyRefreshPending, 0) != 0)
                SchedulePolicyRefresh();
        }
        catch (Refit.ApiException api) when (api.StatusCode
            is System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.NotFound)
        {
            // Credential revoked or the device was deleted from the list while running -
            // enrol again on the next tick instead of looping on the same failure.
            _registeredBranchId = null;
            throw;
        }
    }

    private IReadOnlyList<PrinterEndpointRegistration> BuildEndpoints()
    {
        lock (_endpointCacheLock)
        {
            if (!_endpointsInvalidated
                && DateTime.UtcNow - _endpointsLoadedAt < TimeSpan.FromSeconds(60))
                return _cachedEndpoints;

            try { _printer.EnsureAutoSetup(); } catch { }
            var settings = _printer.GetSettings();
            var endpoints = new Dictionary<string, PrintCapability>(StringComparer.OrdinalIgnoreCase);
            Add(endpoints, settings.BarcodePrinter, PrintCapability.BarcodeLabel);
            Add(endpoints, settings.DocumentPrinter, PrintCapability.Document);
            Add(endpoints, _printer.ReceiptTarget().Printer, PrintCapability.Receipt);
            Add(endpoints, _printer.ProformaTarget(ProformaPrintOptions.Resolve(settings)).Printer, PrintCapability.CartProforma);
            Add(endpoints, _printer.ZReportTarget().Printer, PrintCapability.ZReport);

            _cachedEndpoints = endpoints.Select(x => new PrinterEndpointRegistration(
                StableKey(x.Key),
                x.Key,
                x.Key,
                x.Value,
                _printer.GetPrinterStatus(x.Key),
                JsonSerializer.Serialize(new { configured = true }))).ToList();
            _endpointsLoadedAt = DateTime.UtcNow;
            _endpointsInvalidated = false;
            return _cachedEndpoints;
        }
    }

    private void InvalidateEndpointCache()
    {
        _lastHeartbeatAt = DateTime.MinValue;
        lock (_endpointCacheLock)
            _endpointsInvalidated = true;
        Interlocked.Exchange(ref _endpointPolicyRefreshPending, 1);
    }

    private void SchedulePolicyRefresh()
    {
        Interlocked.Exchange(ref _endpointPolicyRefreshPending, 0);
        _policyCache.Invalidate();
        _ = RefreshPolicySafeAsync();
    }

    private async Task RefreshPolicySafeAsync()
    {
        try
        {
            await _policyCache.RefreshAsync();
        }
        catch
        {
            _policyCache.Invalidate();
        }
    }

    private static void Add(IDictionary<string, PrintCapability> endpoints, string? printer, PrintCapability capability)
    {
        if (string.IsNullOrWhiteSpace(printer)) return;
        if (printer.Contains("Print to PDF", StringComparison.OrdinalIgnoreCase) ||
            printer.Contains("XPS Document", StringComparison.OrdinalIgnoreCase) ||
            printer.Contains("OneNote", StringComparison.OrdinalIgnoreCase)) return;
        endpoints[printer] = endpoints.TryGetValue(printer, out var current) ? current | capability : capability;
    }

    private static string StableKey(string printerName)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(printerName.Trim().ToUpperInvariant()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private async Task<bool> EnsureHubAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_hostToken)) return false;
        if (_connection is null)
        {
            _connection = HubConnections.Create("/hubs/printing", _auth);
            _connection.On<long>("PrintJobAvailable", ignoredJobId =>
            {
                _ = ProcessAssignedAsync(CancellationToken.None);
            });
            _connection.Reconnected += _ => OnHubReconnectedAsync();
        }
        return await _subscription.EnsureAsync(_connection, () =>
            _connection.InvokeAsync("Subscribe", _auth.DeviceId, _hostToken, cancellationToken));
    }

    private async Task OnHubReconnectedAsync()
    {
        try
        {
            if (await EnsureHubAsync(CancellationToken.None))
                await ProcessAssignedAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            ReportHostFailure(exception);
        }
    }

    private async Task ProcessAssignedAsync(CancellationToken cancellationToken)
    {
        if (!_auth.IsAuthenticated || !_auth.HasPermission("printing.host")
            || !SettingsService.Instance.IsFeatureOn("remote_printing")) return;
        Interlocked.Exchange(ref _processPending, 1);
        if (!_processLock.Wait(0)) return;
        try
        {
            do
            {
                Interlocked.Exchange(ref _processPending, 0);
                var jobs = await _printingApi.GetAssignedAsync(_auth.DeviceId, _hostToken!, cancellationToken);
                foreach (var job in jobs)
                    await ProcessAsync(job, cancellationToken);
            }
            while (Volatile.Read(ref _processPending) == 1);
        }
        catch (Exception exception)
        {
            ReportHostFailure(exception);
        }
        finally
        {
            _processLock.Release();
        }
        if (Volatile.Read(ref _processPending) == 1)
            await ProcessAssignedAsync(cancellationToken);
    }

    private async Task ProcessAsync(AssignedPrintJobDto job, CancellationToken cancellationToken)
    {
        var lease = new PrintJobLeaseRequest(_auth.DeviceId, job.LeaseToken, _hostToken!);
        var journalKey = $"{job.Id}:{job.SourceType}:{job.SourceId}";
        var journalState = await _journal.GetStateAsync(journalKey);
        if (journalState == PrintHostJournalState.Completed)
        {
            await _printingApi.AcceptAsync(job.Id, lease, cancellationToken);
            await _printingApi.SubmittedAsync(job.Id, new PrintJobSubmittedRequest(_auth.DeviceId, job.LeaseToken, _hostToken!, null), cancellationToken);
            await _printingApi.CompleteAsync(job.Id, lease, cancellationToken);
            return;
        }
        if (journalState == PrintHostJournalState.Started)
        {
            await _printingApi.AcceptAsync(job.Id, lease, cancellationToken);
            await _printingApi.FailAsync(job.Id, new PrintJobFailedRequest(
                _auth.DeviceId,
                job.LeaseToken,
                _hostToken!,
                "local_print_result_unknown",
                "The desktop process stopped after printing started.",
                true), cancellationToken);
            return;
        }
        // The reported status is not trusted as a gate: a sleeping network printer says
        // "offline" yet prints as soon as data arrives, and the spooler queues for the
        // rest. Real failures still surface through the failure path below.
        var printingStarted = false;
        try
        {
            await _printingApi.AcceptAsync(job.Id, lease, cancellationToken);
            var execute = await PrepareAsync(job, cancellationToken);
            await _journal.MarkStartedAsync(journalKey);
            printingStarted = true;
            execute();
            await _printingApi.SubmittedAsync(job.Id, new PrintJobSubmittedRequest(_auth.DeviceId, job.LeaseToken, _hostToken!, null), cancellationToken);
            await _journal.MarkCompletedAsync(journalKey);
            await _printingApi.CompleteAsync(job.Id, lease, cancellationToken);
        }
        catch (Exception exception)
        {
            if (await _journal.GetStateAsync(journalKey) == PrintHostJournalState.Completed) return;
            try
            {
                await _printingApi.FailAsync(job.Id, new PrintJobFailedRequest(
                    _auth.DeviceId,
                    job.LeaseToken,
                    _hostToken!,
                    "desktop_print_failed",
                    exception.Message,
                    printingStarted), cancellationToken);
            }
            catch
            {
            }
        }
    }

    private Task<Action> PrepareAsync(AssignedPrintJobDto job, CancellationToken cancellationToken)
    {
        return job.Kind switch
        {
            PrintJobKind.Receipt => PrepareReceiptAsync(job, cancellationToken),
            PrintJobKind.BarcodeLabel => Task.FromResult(PrepareBarcode(job)),
            PrintJobKind.ZReport => PrepareZReportAsync(job),
            PrintJobKind.Document => PrepareDocumentAsync(job, cancellationToken),
            PrintJobKind.CartProforma => PrepareCartProformaAsync(job, cancellationToken),
            _ => throw new InvalidOperationException("Unsupported print type.")
        };
    }

    private bool IsDocumentPrinter(string? printerName) =>
        !string.IsNullOrWhiteSpace(printerName)
        && _printer.KindOf(printerName) is not (PrinterKind.ReceiptThermal or PrinterKind.Label);

    private async Task<Action> PrepareReceiptAsync(AssignedPrintJobDto job, CancellationToken cancellationToken)
    {
        var token = Text(job.Payload, "receiptToken") ?? job.SourceId;
        var actualPrinter = string.IsNullOrWhiteSpace(job.PrinterSystemName)
            ? _printer.ReceiptTarget().Printer
            : job.PrinterSystemName;

        if (job.SourceType == "customer_return")
            return await PrepareReturnAsync(job, actualPrinter, cancellationToken);

        if (job.SourceType is "customer_payment" or "customer_refund")
            return await PrepareMoneyDocumentAsync(job, actualPrinter, cancellationToken);

        // The render style follows the physical printer the job will come out of.
        if (IsDocumentPrinter(actualPrinter))
        {
            var pages = await LoadReceiptDocumentAsync(
                token,
                cancellationToken,
                job.Id,
                monochrome: !_printer.GetPrinterCapabilities(actualPrinter).SupportsColor);
            var pdfPath = await GetPdfOutputPathAsync(actualPrinter, $"Chek_{token}");
            return () => _printer.PrintDocumentImages(pages, actualPrinter ?? "", job.Copies, pdfPath);
        }
        var receipt = await _receiptApi.GetAsync(token);
        var receiptOptions = ReceiptOptions(job.Payload, _printer.ReceiptOptions);
        
        if (receiptOptions?.ShowLogo == true && !string.IsNullOrWhiteSpace(receipt.LogoImageKey))
        {
            var logoKey = !string.IsNullOrWhiteSpace(receipt.MonochromeLogoImageKey)
                ? receipt.MonochromeLogoImageKey
                : receipt.LogoImageKey;
            var width = _printer.ReceiptRasterWidth(actualPrinter, receiptOptions.Width);
            var raster = await _logoCache.GetForPrintAsync(logoKey!, width, cancellationToken);
            if (raster is null)
                _toast.Warning(LocalizationManager.Instance["print_logo_unavailable"]);
            else
                receiptOptions = receiptOptions with { LogoRasterBytes = raster };
        }

        var receiptFilePath = await GetPdfOutputPathAsync(actualPrinter, $"Chek_{token}");
        if (receiptOptions != null)
            receiptOptions = receiptOptions with { OutputFilePath = receiptFilePath };

        return () => _printer.PrintReceipt(receipt, actualPrinter ?? "", job.Copies, receiptOptions);
    }

    private async Task<Action> PrepareReturnAsync(AssignedPrintJobDto job, string? actualPrinter, CancellationToken cancellationToken)
    {
        if (!long.TryParse(job.SourceId, out var returnId) || returnId <= 0)
            throw new InvalidOperationException("Return document is required.");
        var document = await _returnsApi.GetByIdAsync(returnId);
        var receiptOptions = ReceiptOptions(job.Payload, _printer.ReceiptOptions);

        BusinessDto? business = null;
        try { business = await _businessApi.GetAsync(); } catch { }

        var logoKey = !string.IsNullOrWhiteSpace(business?.MonochromeLogoImageKey)
            ? business.MonochromeLogoImageKey
            : business?.LogoImageKey;
        if (receiptOptions?.ShowLogo == true && !string.IsNullOrWhiteSpace(logoKey))
        {
            var width = _printer.ReceiptRasterWidth(actualPrinter, receiptOptions.Width);
            var raster = await _logoCache.GetForPrintAsync(logoKey, width, cancellationToken);
            if (raster is null)
                _toast.Warning(LocalizationManager.Instance["print_logo_unavailable"]);
            else
                receiptOptions = receiptOptions with { LogoRasterBytes = raster };
        }

        var returnFilePath = await GetPdfOutputPathAsync(actualPrinter, $"Qaytarish_{returnId}");
        if (receiptOptions != null)
            receiptOptions = receiptOptions with { OutputFilePath = returnFilePath };

        // A shop whose receipts come out of a document printer gets the return on the same
        // printer as a document page instead of raw thermal bytes it cannot render.
        if (IsDocumentPrinter(actualPrinter))
        {
            var settings = _printer.GetSettings();
            var preview = new PreviewDocument(
                document.CreatedAt.ToLocalTime(),
                document.UserName,
                document.CustomerName,
                [.. document.Lines.Select(x => new PreviewLine(
                    x.ProductName, x.Quantity, x.UnitName, x.UnitPrice, x.LineAmount))],
                0,
                document.RefundAmount,
                document.Note);
            var paper = settings.DocumentPaperSize == "a5" ? "a5" : "a4";
            var pages = ProformaDocumentRenderer.Render(
                preview,
                $"Hujjat № {document.DocumentNumber}",
                business,
                new ProformaPrintOptions(receiptOptions?.HeaderText, receiptOptions?.FooterText, 32, "A4"),
                paper,
                _printer.GetPrinterCapabilities(actualPrinter).SupportsColor,
                "MAHSULOT QAYTARISH");
            return () => WindowsImagePrinter.Print(actualPrinter!, pages, paper, paper, "portrait", 1, job.Copies, returnFilePath);
        }

        return () => _printer.PrintReturn(document, actualPrinter ?? "", job.Copies, receiptOptions, business);
    }

    /// A payment and a payout are the same slip with opposite signs, so both are rendered from one
    /// shape instead of two near-identical formatters that would drift apart.
    private async Task<Action> PrepareMoneyDocumentAsync(
        AssignedPrintJobDto job, string? actualPrinter, CancellationToken cancellationToken)
    {
        if (!long.TryParse(job.SourceId, out var documentId) || documentId <= 0)
            throw new InvalidOperationException("Money document is required.");

        var isPayout = job.SourceType == "customer_refund";
        MoneyDocument document;
        if (isPayout)
        {
            var refund = await _refundsApi.GetByIdAsync(documentId);
            document = new MoneyDocument(
                refund.DocumentNumber, refund.CreatedAt, refund.UserName, refund.CustomerName,
                refund.TotalBaseAmount, refund.BalanceAfterBase, refund.Note,
                [.. refund.Tenders.Select(x => new MoneyLine(x.Method, x.Currency, x.Amount, x.AmountBase))],
                refund.AdvanceBaseAmount, refund.LoanBaseAmount, 0);
        }
        else
        {
            var payment = await _paymentsApi.GetByIdAsync(documentId);
            document = new MoneyDocument(
                payment.DocumentNumber, payment.CreatedAt, payment.UserName, payment.CustomerName,
                payment.TotalBaseAmount, payment.BalanceAfterBase, payment.Note,
                [.. payment.Tenders.Select(x => new MoneyLine(x.Method, x.Currency, x.Amount, x.AmountBase))],
                payment.AdvanceBaseAmount, 0, payment.WriteOffBaseAmount);
        }

        var receiptOptions = ReceiptOptions(job.Payload, _printer.ReceiptOptions);
        BusinessDto? business = null;
        try { business = await _businessApi.GetAsync(); } catch { }

        var path = await GetPdfOutputPathAsync(actualPrinter, $"{(isPayout ? "Chiqim" : "Tolov")}_{document.Number}");
        if (receiptOptions != null)
            receiptOptions = receiptOptions with { OutputFilePath = path };

        return () => _printer.PrintMoneyDocument(
            document, isPayout, actualPrinter ?? "", job.Copies, receiptOptions, business);
    }

    private Action PrepareBarcode(AssignedPrintJobDto job)
    {
        var code = Text(job.Payload, "code") ?? throw new InvalidOperationException("Barcode is required.");
        var name = Text(job.Payload, "name") ?? throw new InvalidOperationException("Product name is required.");
        var priceText = Text(job.Payload, "priceText");
        var withPrice = Boolean(job.Payload, "withPrice") ?? _printer.GetSettings().LabelDefaultWithPrice;
        var settings = _printer.GetSettings();
        var localOptions = LabelSize.Resolve(settings);
        var options = localOptions with
        {
            NameLines = (int)Math.Clamp(Number(job.Payload, "nameLines") ?? localOptions.NameLines, 0, 2),
            ShowSku = Boolean(job.Payload, "showSku") ?? localOptions.ShowSku
        };
        return () => _labels.PrintLabels(code, name, job.Copies, job.PrinterSystemName,
            withPrice ? priceText : null, Text(job.Payload, "sku"), options);
    }

    private async Task<Action> PrepareZReportAsync(AssignedPrintJobDto job)
    {
        var value = Number(job.Payload, "shiftId") ?? (long.TryParse(job.SourceId, out var id) ? id : 0);
        if (value <= 0) throw new InvalidOperationException("Shift is required.");
        var report = await _shiftsApi.GetReportAsync(value);
        var pdfPath = await GetPdfOutputPathAsync(job.PrinterSystemName, $"ZReport_{value}");
        return () => _printer.PrintZReport(report, job.PrinterSystemName, job.Copies, pdfPath);
    }

    private async Task<Action> PrepareDocumentAsync(AssignedPrintJobDto job, CancellationToken cancellationToken)
    {
        var receiptToken = Text(job.Payload, "receiptToken");
        if (string.IsNullOrWhiteSpace(receiptToken))
            throw new InvalidOperationException("Unsupported document source.");
        var pages = await LoadReceiptDocumentAsync(
            receiptToken,
            cancellationToken,
            job.Id,
            monochrome: !_printer.GetPrinterCapabilities(job.PrinterSystemName).SupportsColor);
        var pdfPath = await GetPdfOutputPathAsync(job.PrinterSystemName, $"Hujjat_{receiptToken}");
        return () => _printer.PrintDocumentImages(pages, job.PrinterSystemName, job.Copies, pdfPath);
    }

    private async Task<Action> PrepareCartProformaAsync(AssignedPrintJobDto job, CancellationToken cancellationToken)
    {
        var cartCode = Text(job.Payload, "cartCode") ?? job.SourceId;
        var cart = await _orderingApi.GetByCodeAsync(cartCode);
        var document = new PreviewDocument(
            DateTime.Now,
            _auth.UserInfo?.FullName,
            cart.CustomerName,
            [.. cart.Items.Select(x => new PreviewLine(
                x.ProductName, x.Quantity, x.UnitName, x.UnitPrice, x.LineTotal))],
            0,
            cart.Total,
            cart.Note);
        var options = ProformaOptions(job.Payload, _printer.GetSettings());
        var target = _printer.ProformaTarget(options);
        var printer = string.IsNullOrWhiteSpace(job.PrinterSystemName) ? target.Printer : job.PrinterSystemName;
        if (string.IsNullOrWhiteSpace(printer))
            throw new InvalidOperationException("Chek printeri tanlanmagan.");

        BusinessDto? business = null;
        try { business = await _businessApi.GetAsync(); } catch { }
        var path = await GetPdfOutputPathAsync(printer, $"Oldindan_{job.Id}");
        if (IsDocumentPrinter(printer))
        {
            var pages = ProformaDocumentRenderer.Render(
                document, $"Savat: {cartCode}", business, options, target.Paper,
                _printer.GetPrinterCapabilities(printer).SupportsColor);
            return () => WindowsImagePrinter.Print(printer, pages, target.Paper, target.Paper, "portrait", 1, job.Copies, path);
        }
        if (!ReceiptPaper.IsValid(options.Width))
            options = options with { Width = _printer.ReceiptWidth(printer) };
        var bytes = _printer.FormatProforma(document, cartCode, options, business);
        return () => _printer.PrintRawBytes(printer, bytes, path);
    }

    private static ProformaPrintOptions ProformaOptions(JsonElement payload, PrinterSettings fallback)
    {
        if (!payload.TryGetProperty("proformaSettings", out var settings) || settings.ValueKind != JsonValueKind.Object)
            return ProformaPrintOptions.Resolve(fallback);
        return new ProformaPrintOptions(
            Text(settings, "headerText"),
            Text(settings, "footerText"),
            ReceiptPaper.Sanitize((int)(Number(settings, "paperWidth") ?? 32)),
            Text(settings, "paperFormat") ?? "Thermal",
            Boolean(settings, "showBusinessName") ?? true,
            Boolean(settings, "showAddress") ?? true,
            Boolean(settings, "showPhone") ?? true,
            Boolean(settings, "showSeller") ?? true,
            Boolean(settings, "showCustomer") ?? true,
            Boolean(settings, "showNote") ?? true,
            Boolean(settings, "showCartCode") ?? true);
    }

    private async Task<IReadOnlyList<byte[]>> LoadReceiptDocumentAsync(
        string token,
        CancellationToken cancellationToken,
        long? printJobId = null,
        bool monochrome = false)
    {
        var settings = _printer.GetSettings();
        var format = DocumentPrintLayout.ResolveOutputFormat("document", settings.DocumentPaperSize ?? "a4");
        var orientation = DocumentPrintLayout.GetReceiptOrientation(
            settings.DocumentOrientation is "landscape" ? "landscape" : "portrait",
            settings.DocumentPagesPerSheet);
        var content = await _receiptApi.GetPrintImagesAsync(token, format, orientation, printJobId, monochrome);
        await using var package = await content.ReadAsStreamAsync(cancellationToken);
        using var archive = new ZipArchive(package, ZipArchiveMode.Read);
        var pages = new List<byte[]>(archive.Entries.Count);
        foreach (var entry in archive.Entries.OrderBy(x => x.FullName, StringComparer.Ordinal))
        {
            await using var input = entry.Open();
            using var output = new MemoryStream();
            await input.CopyToAsync(output, cancellationToken);
            pages.Add(output.ToArray());
        }
        return pages;
    }

    private async Task<string?> GetPdfOutputPathAsync(string? printerName, string defaultFileName)
    {
        if (string.IsNullOrWhiteSpace(printerName) || 
            (!printerName.Contains("Print to PDF", StringComparison.OrdinalIgnoreCase) &&
             !printerName.Contains("Save to PDF", StringComparison.OrdinalIgnoreCase) &&
             !printerName.Contains("XPS", StringComparison.OrdinalIgnoreCase) &&
             !printerName.Contains("OneNote", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        var path = _printer.GetSettings().PdfExportPath;
        if (!string.IsNullOrWhiteSpace(path))
        {
            if (!System.IO.Directory.Exists(path))
                System.IO.Directory.CreateDirectory(path);
            return System.IO.Path.Combine(path, $"{defaultFileName}.pdf");
        }
        string? picked = null;
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () => 
        {
            picked = await _filePicker.SaveFilePathAsync(defaultFileName, "pdf");
        });
            
        if (string.IsNullOrWhiteSpace(picked))
            throw new OperationCanceledException("PDF saqlash bekor qilindi.");
                
        return picked;
    }

    private static string? Text(JsonElement payload, string name) =>
        payload.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static long? Number(JsonElement payload, string name) =>
        payload.TryGetProperty(name, out var value) && value.TryGetInt64(out var number) ? number : null;

    private static bool? Boolean(JsonElement payload, string name) =>
        payload.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;

    private static ReceiptPrintOptions? ReceiptOptions(JsonElement payload, ReceiptPrintOptions? fallback)
    {
        if (!payload.TryGetProperty("receiptSettings", out var settings) || settings.ValueKind != JsonValueKind.Object)
            return fallback;
        return new ReceiptPrintOptions(
            Text(settings, "headerText"),
            Text(settings, "footerText"),
            ReceiptPaper.Sanitize((int)(Number(settings, "paperWidth") ?? fallback?.Width ?? 32)),
            Boolean(settings, "showBusinessName") ?? true,
            Boolean(settings, "showBranchName") ?? true,
            Boolean(settings, "showAddress") ?? true,
            Boolean(settings, "showPhone") ?? true,
            Boolean(settings, "showCashier") ?? true,
            Boolean(settings, "showCustomer") ?? true,
            Boolean(settings, "showReceiptNumber") ?? true,
            Boolean(settings, "showPaymentDetails") ?? true,
            Boolean(settings, "showQrCode") ?? true,
            Boolean(settings, "showElectronicLink") ?? true,
            Text(settings, "publicReceiptBaseUrl"),
            Boolean(settings, "showLogo") ?? true,
            Boolean(settings, "showCustomerPhone") ?? true,
            Boolean(settings, "showCustomerEmail") ?? false,
            Template: fallback?.Template ?? "auto");
    }

    private void BranchChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(BranchContextService.SelectedBranch)) return;
        _registeredBranchId = null;
        _ = ProcessAssignedAsync(CancellationToken.None);
    }

    private async Task StopAsync()
    {
        await _startLock.WaitAsync();
        var lifetime = _lifetime;
        var connection = _connection;
        _lifetime = null;
        _connection = null;
        _registeredBranchId = null;
        _subscription.Invalidate();
        _startLock.Release();
        lifetime?.Cancel();
        lifetime?.Dispose();
        if (connection is null) return;
        try { await connection.DisposeAsync(); } catch { }
    }
}
