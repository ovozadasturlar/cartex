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
    private static readonly HttpClient ImageHttpClient = new();
    private readonly IPrintingApi _printingApi;
    private readonly IReceiptApi _receiptApi;
    private readonly IShiftsApi _shiftsApi;
    private readonly ICustomerReturnsApi _returnsApi;
    private readonly IBusinessApi _businessApi;
    private readonly IPrinterService _printer;
    private readonly IBarcodeLabelService _labels;
    private readonly AuthService _auth;
    private readonly BranchContextService _branch;
    private readonly PrintHostJournal _journal;
    private readonly PrintHostCredentialStore _credentialStore;
    private readonly IFilePickerService _filePicker;
    private readonly SemaphoreSlim _startLock = new(1, 1);
    private readonly SemaphoreSlim _processLock = new(1, 1);
    private CancellationTokenSource? _lifetime;
    private HubConnection? _connection;
    private long? _registeredBranchId;
    private string? _hostToken;

    public PrintHostService(
        IPrintingApi printingApi,
        IReceiptApi receiptApi,
        IShiftsApi shiftsApi,
        ICustomerReturnsApi returnsApi,
        IBusinessApi businessApi,
        IPrinterService printer,
        IBarcodeLabelService labels,
        AuthService auth,
        BranchContextService branch,
        PrintHostJournal journal,
        PrintHostCredentialStore credentialStore,
        IFilePickerService filePicker)
    {
        _printingApi = printingApi;
        _receiptApi = receiptApi;
        _shiftsApi = shiftsApi;
        _returnsApi = returnsApi;
        _businessApi = businessApi;
        _printer = printer;
        _labels = labels;
        _auth = auth;
        _branch = branch;
        _journal = journal;
        _credentialStore = credentialStore;
        _filePicker = filePicker;
        _hostToken = credentialStore.Load();
        _auth.LoggedOut += () => _ = StopAsync();
        _branch.PropertyChanged += BranchChanged;
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

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!_auth.IsAuthenticated || !_auth.HasPermission("printing.host"))
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                    continue;
                }

                if (_branch.CurrentBranchId is null)
                {
                    await Task.Delay(1000, cancellationToken);
                    continue;
                }
                await RegisterAsync(_branch.CurrentBranchId.Value, cancellationToken);
                await EnsureHubAsync(cancellationToken);

                // If SignalR is disconnected, fallback to HTTP polling
                if (_connection is null || _connection.State != HubConnectionState.Connected)
                {
                    await ProcessAssignedAsync(cancellationToken);
                }

                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
            }
        }
    }

    private async Task RegisterAsync(long branchId, CancellationToken cancellationToken)
    {
        var endpoints = BuildEndpoints();
        if (_registeredBranchId != branchId)
        {
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
            return;
        }
        await _printingApi.HeartbeatAsync(new PrintNodeHeartbeatRequest(
            _auth.DeviceId,
            endpoints,
            _hostToken ?? throw new InvalidOperationException("Print host credential is missing.")), cancellationToken);
    }

    private IReadOnlyList<PrinterEndpointRegistration> BuildEndpoints()
    {
        var settings = _printer.GetSettings();
        var endpoints = new Dictionary<string, PrintCapability>(StringComparer.OrdinalIgnoreCase);
        Add(endpoints, settings.BarcodePrinter, PrintCapability.BarcodeLabel);
        Add(endpoints, settings.DocumentPrinter, PrintCapability.Document);
        Add(endpoints,
            settings.ReceiptMode is "a4" or "a5" ? settings.DocumentPrinter : settings.ReceiptPrinter,
            PrintCapability.Receipt);
        Add(endpoints,
            string.IsNullOrWhiteSpace(settings.ZReportPrinter)
                ? settings.ZReportMode is "a4" or "a5" ? settings.DocumentPrinter : settings.ReceiptPrinter
                : settings.ZReportPrinter,
            PrintCapability.ZReport);

        return endpoints.Select(x => new PrinterEndpointRegistration(
            StableKey(x.Key),
            x.Key,
            x.Key,
            x.Value,
            _printer.GetPrinterStatus(x.Key),
            JsonSerializer.Serialize(new { configured = true }))).ToList();
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

    private async Task EnsureHubAsync(CancellationToken cancellationToken)
    {
        if (_connection is null)
        {
            _connection = new HubConnectionBuilder()
                .WithUrl(SettingsService.Instance.ApiBaseUrl.TrimEnd('/') + "/hubs/printing", options =>
                {
                    options.AccessTokenProvider = () => _auth.EnsureFreshTokenAsync(CancellationToken.None);
                    options.Headers["X-Client"] = "desktop";
                    options.Headers["X-Device-Id"] = _auth.DeviceId;
                    options.Headers["X-Device-Name"] = _auth.DeviceName;
                })
                .WithAutomaticReconnect()
                .Build();
            _connection.On<long>("PrintJobAvailable", jobId =>
            {
                _ = ProcessAssignedAsync(CancellationToken.None);
            });
            _connection.Reconnected += async _ =>
            {
                await _connection.InvokeAsync("Subscribe", _auth.DeviceId, _hostToken);
                await ProcessAssignedAsync(CancellationToken.None);
            };
        }
        if (_connection.State == HubConnectionState.Disconnected)
        {
            await _connection.StartAsync(cancellationToken);
            await _connection.InvokeAsync("Subscribe", _auth.DeviceId, _hostToken, cancellationToken);
        }
    }

    private async Task ProcessAssignedAsync(CancellationToken cancellationToken)
    {
        if (!_processLock.Wait(0)) return;
        try
        {
            var jobs = await _printingApi.GetAssignedAsync(_auth.DeviceId, _hostToken!, cancellationToken);
            foreach (var job in jobs)
                await ProcessAsync(job, cancellationToken);
        }
        catch
        {
        }
        finally
        {
            _processLock.Release();
        }
    }

    private async Task ProcessAsync(AssignedPrintJobDto job, CancellationToken cancellationToken)
    {
        var lease = new PrintJobLeaseRequest(_auth.DeviceId, job.LeaseToken, _hostToken!);
        var journalState = await _journal.GetStateAsync(job.Id);
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
        var printerStatus = _printer.GetPrinterStatus(job.PrinterSystemName);
        if (printerStatus is not (PrinterEndpointStatus.Ready or PrinterEndpointStatus.Busy))
        {
            await _printingApi.FailAsync(job.Id, new PrintJobFailedRequest(
                _auth.DeviceId,
                job.LeaseToken,
                _hostToken!,
                "printer_unavailable",
                $"Printer '{job.PrinterDisplayName}' is {printerStatus}.",
                false), cancellationToken);
            return;
        }
        var printingStarted = false;
        try
        {
            await _printingApi.AcceptAsync(job.Id, lease, cancellationToken);
            var execute = await PrepareAsync(job, cancellationToken);
            await _journal.MarkStartedAsync(job.Id);
            printingStarted = true;
            execute();
            await _printingApi.SubmittedAsync(job.Id, new PrintJobSubmittedRequest(_auth.DeviceId, job.LeaseToken, _hostToken!, null), cancellationToken);
            await _journal.MarkCompletedAsync(job.Id);
            await _printingApi.CompleteAsync(job.Id, lease, cancellationToken);
        }
        catch (Exception exception)
        {
            if (await _journal.GetStateAsync(job.Id) == PrintHostJournalState.Completed) return;
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
            _ => throw new InvalidOperationException("Unsupported print type.")
        };
    }

    private async Task<Action> PrepareReceiptAsync(AssignedPrintJobDto job, CancellationToken cancellationToken)
    {
        var token = Text(job.Payload, "receiptToken") ?? job.SourceId;
        var settings = _printer.GetSettings();
        var actualPrinter = string.IsNullOrWhiteSpace(job.PrinterSystemName) 
            ? settings.ReceiptPrinter 
            : job.PrinterSystemName;
            
        var isPdfPrinter = actualPrinter != null &&
                           (actualPrinter.Contains("Print to PDF", StringComparison.OrdinalIgnoreCase) ||
                            actualPrinter.Contains("Save to PDF", StringComparison.OrdinalIgnoreCase) ||
                            actualPrinter.Contains("XPS", StringComparison.OrdinalIgnoreCase) ||
                            actualPrinter.Contains("OneNote", StringComparison.OrdinalIgnoreCase));

        if (job.SourceType == "customer_return")
            return await PrepareReturnAsync(job, actualPrinter, cancellationToken);

        if (settings.ReceiptMode is "a4" or "a5" || isPdfPrinter)
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
            try
            {
                var storageApi = Avalonia.Controls.Design.IsDesignMode ? null : ServiceLocator.Resolve<IStorageApi>();
                if (storageApi != null)
                {
                    var logoKey = !string.IsNullOrWhiteSpace(receipt.MonochromeLogoImageKey)
                        ? receipt.MonochromeLogoImageKey
                        : receipt.LogoImageKey;
                    var file = await storageApi.GetUrlAsync(logoKey!);
                    var imageBytes = await ImageHttpClient.GetByteArrayAsync(ImageUrl.Absolute(file.Url), cancellationToken);
                    
                    int width = receiptOptions.Width is 48 ? 576 : (receiptOptions.Width is 42 ? 504 : 384);
                    var rasterBytes = EscPosImageHelper.BinarizeToEscPosRaster(imageBytes, width);
                    receiptOptions = receiptOptions with { LogoRasterBytes = rasterBytes };
                }
            }
            catch { }
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
            try
            {
                var storageApi = Avalonia.Controls.Design.IsDesignMode ? null : ServiceLocator.Resolve<IStorageApi>();
                if (storageApi != null)
                {
                    var file = await storageApi.GetUrlAsync(logoKey!);
                    var imageBytes = await ImageHttpClient.GetByteArrayAsync(ImageUrl.Absolute(file.Url), cancellationToken);

                    int width = receiptOptions.Width is 48 ? 576 : (receiptOptions.Width is 42 ? 504 : 384);
                    var rasterBytes = EscPosImageHelper.BinarizeToEscPosRaster(imageBytes, width);
                    receiptOptions = receiptOptions with { LogoRasterBytes = rasterBytes };
                }
            }
            catch { }
        }

        var returnFilePath = await GetPdfOutputPathAsync(actualPrinter, $"Qaytarish_{returnId}");
        if (receiptOptions != null)
            receiptOptions = receiptOptions with { OutputFilePath = returnFilePath };

        return () => _printer.PrintReturn(document, actualPrinter ?? "", job.Copies, receiptOptions, business);
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
            (int)Math.Clamp(Number(settings, "paperWidth") ?? fallback?.Width ?? 32, 24, 120),
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
            Boolean(settings, "showCustomerEmail") ?? false);
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
        _startLock.Release();
        lifetime?.Cancel();
        lifetime?.Dispose();
        if (connection is null) return;
        try { await connection.DisposeAsync(); } catch { }
    }
}
