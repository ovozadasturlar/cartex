using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Cartex.Shared.Localization;
using Cartex.Shared.Models.Business;
using Cartex.Shared.Models.Printing;
using Cartex.Shared.Models.Sales;
using Cartex.Shared.Models.Settings;
using Cartex.Shared.Models.Shifts;

namespace Cartex.UI.Services;

public sealed record PrinterSettings
{
    public string? ReceiptPrinter { get; init; }
    public string? ZReportPrinter { get; init; }
    public string? BarcodePrinter { get; init; }
    public string? DocumentPrinter { get; init; }
    public string? PdfExportPath { get; init; }
    public bool AutoPrintReceipt { get; init; }
    public double LabelWidthMm { get; init; }
    public double LabelHeightMm { get; init; }
    public string? ReceiptMode { get; init; }
    public int ReceiptPaperWidth { get; init; }
    public int ReceiptCopies { get; init; } = 1;
    public string? ReceiptHeaderText { get; init; }
    public string? ReceiptFooterText { get; init; }
    public int ReceiptContentWidth { get; init; } = 32;
    public bool ReceiptShowBusinessName { get; init; } = true;
    public bool ReceiptShowBranchName { get; init; } = true;
    public bool ReceiptShowAddress { get; init; } = true;
    public bool ReceiptShowPhone { get; init; } = true;
    public bool ReceiptShowCashier { get; init; } = true;
    public bool ReceiptShowCustomer { get; init; } = true;
    public bool ReceiptShowNumber { get; init; } = true;
    public bool ReceiptShowPaymentDetails { get; init; } = true;
    public bool ReceiptShowQrCode { get; init; } = true;
    public bool ReceiptShowElectronicLink { get; init; } = true;
    public bool ReceiptShowLogo { get; init; } = true;
    public bool ReceiptShowCustomerPhone { get; init; } = true;
    public bool ReceiptShowCustomerEmail { get; init; }
    public string? ReceiptPublicBaseUrl { get; init; }
    public long? CachedPrintingBranchId { get; init; }
    public string? CachedPrintingRevision { get; init; }
    public DateTime? PrintingLastSyncedAtUtc { get; init; }
    public bool CentralAutoPrint { get; init; }
    public bool AutoPrintZReport { get; init; }
    public string? LabelMode { get; init; }
    public double LabelGapMm { get; init; }
    public int LabelDpi { get; init; }
    public double LabelShiftXMm { get; init; }
    public double LabelShiftYMm { get; init; }
    public int LabelRotation { get; init; } = -1;
    public int LabelDensity { get; init; }
    public int LabelSpeed { get; init; }
    public bool UsePrinterGapCalibration { get; init; }
    public string? LabelCurrencyDisplay { get; init; }
    public string? LabelCurrencyCase { get; init; }
    public string? LabelPriceCurrencyMode { get; init; }
    public int LabelNameLines { get; init; } = 2;
    public bool LabelDefaultWithPrice { get; init; }
    public bool LabelAllowPriceOverride { get; init; } = true;
    public bool LabelShowSku { get; init; }
    public string? DocumentPaperSize { get; init; }
    public string? DocumentOrientation { get; init; }
    public int DocumentPagesPerSheet { get; init; } = 1;
    public string? ProformaPaperFormat { get; init; }
    public int ProformaPaperWidth { get; init; } = 32;
    public string? ProformaHeaderText { get; init; }
    public string? ProformaFooterText { get; init; }
    public string? AutoSetupSignature { get; init; }
    public bool ProformaShowBusinessName { get; init; } = true;
    public bool ProformaShowAddress { get; init; } = true;
    public bool ProformaShowPhone { get; init; } = true;
    public bool ProformaShowSeller { get; init; } = true;
    public bool ProformaShowCustomer { get; init; } = true;
    public bool ProformaShowNote { get; init; } = true;
    public bool ProformaShowCartCode { get; init; } = true;
    public string? ZReportMode { get; init; }
    public int ZReportPaperWidth { get; init; }
    public string? ReceiptLanguage { get; init; }
    public string? ZReportLanguage { get; init; }
    public string? ReceiptCharset { get; init; }
    public int ReceiptCodeTable { get; init; } = -1;
    public string? ReceiptPrintMode { get; init; }
    public string? ReceiptTemplate { get; init; }
    public int ReceiptRasterWidthDots { get; init; }
    /// Termal boshning issiqligi va qog'oz sezgirligi har printerda har xil: bir xil raster
    /// birida yupqa, boshqasida bo'yalib chiqadi. "light" | "medium" | "dark".
    public string? ReceiptDarkness { get; init; }
    public string? ReceiptCutMode { get; init; }
    public int ReceiptFeedBeforeCut { get; init; } = 4;
    /// Yozuv o'lchami ustunlar soni orqali beriladi: kamroq ustun = kattaroq shrift.
    /// "normal" | "large" | "xlarge".
    public string? ReceiptTextSize { get; init; }
    /// Raster sifati: "fast" | "standard" | "high".
    public string? ReceiptQuality { get; init; }
    /// Printer qo'llab-quvvatlamagan kirill harflarini eng yaqiniga almashtirib, matn
    /// rejimida qolish (қ->к, ғ->г, ҳ->х, ў->о).
    public bool ReceiptFoldCyrillic { get; init; }
    public string? ZReportDocumentPaperSize { get; init; }
    public string? ZReportDocumentOrientation { get; init; }
    public int ZReportDocumentPagesPerSheet { get; init; } = 1;
}

public record ReceiptPrintOptions(
    string? HeaderText,
    string? FooterText,
    int Width,
    bool ShowBusinessName = true,
    bool ShowBranchName = true,
    bool ShowAddress = true,
    bool ShowPhone = true,
    bool ShowCashier = true,
    bool ShowCustomer = true,
    bool ShowReceiptNumber = true,
    bool ShowPaymentDetails = true,
    bool ShowQrCode = true,
    bool ShowElectronicLink = true,
    string? PublicReceiptBaseUrl = null,
    bool ShowLogo = true,
    bool ShowCustomerPhone = true,
    bool ShowCustomerEmail = false,
    byte[]? LogoRasterBytes = null,
    string? OutputFilePath = null,
    string Template = "auto");

public record LabelOptions(
    double WidthMm,
    double HeightMm,
    double GapMm,
    int Dpi,
    double ShiftXMm,
    double ShiftYMm,
    int Rotation,
    int Density,
    int Speed,
    bool UsePrinterGapCalibration,
    int NameLines = 2,
    bool ShowSku = false);

public record PrinterCapabilities(bool SupportsColor);

public sealed record DriverPaper(double WidthMm, int Columns);

public static class LabelSize
{
    public static (double Width, double Height) Resolve(double width, double height) =>
        (width is < 20 or > 120 ? 40 : width, height is < 20 or > 120 ? 30 : height);

    public static double ResolveGap(double gap) => gap is < 1 or > 20 ? 4 : gap;

    public static LabelOptions Resolve(PrinterSettings s)
    {
        var (width, height) = Resolve(s.LabelWidthMm, s.LabelHeightMm);
        return new LabelOptions(
            width,
            height,
            ResolveGap(s.LabelGapMm),
            s.LabelDpi is 203 or 300 ? s.LabelDpi : 203,
            Math.Clamp(s.LabelShiftXMm, -10, 10),
            Math.Clamp(s.LabelShiftYMm, -10, 10),
            s.LabelRotation is 0 or 180 ? s.LabelRotation : 180,
            s.LabelDensity is >= 1 and <= 15 ? s.LabelDensity : 8,
            s.LabelSpeed is >= 1 and <= 6 ? s.LabelSpeed : 4,
            s.UsePrinterGapCalibration,
            s.LabelNameLines is 1 or 2 ? s.LabelNameLines : 0,
            s.LabelShowSku);
    }
}

public sealed record PreviewLine(string Name, decimal Quantity, string Unit, decimal UnitPrice, decimal Amount);

public sealed record MoneyLine(string Method, string Currency, decimal Amount, decimal AmountBase);

/// A payment or a payout slip. BalanceAfter is the customer's position at the moment the document
/// was issued, not now — a reprint months later must still say what the customer was told.
public sealed record MoneyDocument(
    string Number,
    DateTime CreatedAt,
    string? UserName,
    string? CustomerName,
    decimal TotalBase,
    decimal BalanceAfter,
    string? Note,
    IReadOnlyList<MoneyLine> Tenders,
    decimal AdvanceBase,
    decimal LoanBase,
    decimal WriteOffBase);

public sealed record PreviewDocument(
    DateTime CreatedAt,
    string? UserName,
    string? CustomerName,
    IReadOnlyList<PreviewLine> Lines,
    decimal Discount,
    decimal Total,
    string? Note);

public sealed record ProformaPrintOptions(
    string? HeaderText,
    string? FooterText,
    int Width,
    string PaperFormat,
    bool ShowBusinessName = true,
    bool ShowAddress = true,
    bool ShowPhone = true,
    bool ShowSeller = true,
    bool ShowCustomer = true,
    bool ShowNote = true,
    bool ShowCartCode = true)
{
    /// Sarlavha/yakuniy matn kiritilmagan bo'lsa chekniki meros bo'ladi — proforma odatda
    /// o'sha chek printeridan chiqadi va bir xil brendlashni kutadi.
    public static ProformaPrintOptions Resolve(PrinterSettings s) => new(
        string.IsNullOrWhiteSpace(s.ProformaHeaderText) ? s.ReceiptHeaderText : s.ProformaHeaderText,
        string.IsNullOrWhiteSpace(s.ProformaFooterText) ? s.ReceiptFooterText : s.ProformaFooterText,
        ReceiptPaper.IsValid(s.ProformaPaperWidth) ? s.ProformaPaperWidth : 0,
        s.ProformaPaperFormat is "A4" or "A5" ? s.ProformaPaperFormat : "Thermal",
        s.ProformaShowBusinessName,
        s.ProformaShowAddress,
        s.ProformaShowPhone,
        s.ProformaShowSeller,
        s.ProformaShowCustomer,
        s.ProformaShowNote,
        s.ProformaShowCartCode);
}

public interface IPrinterService
{
    event Action? SettingsChanged;
    IReadOnlyList<string> GetInstalledPrinters();
    PrinterEndpointStatus GetPrinterStatus(string? printerName);
    PrinterCapabilities GetPrinterCapabilities(string? printerName);
    DriverPaper? DriverReceiptPaper(string? printerName);
    int ReceiptWidth(string? printerName, int? contentWidth = null);
    int ReceiptRasterWidth(string? printerName, int? contentWidth = null);
    void EnsureAutoSetup();
    PrinterKind KindOf(string printerName);
    PrintTarget ReceiptTarget();
    PrintTarget ProformaTarget(ProformaPrintOptions options);
    PrintTarget ZReportTarget();
    PrinterSettings GetSettings();
    void SaveSettings(PrinterSettings settings);
    void CacheBarcodeLabelSettings(BarcodeLabelSettingsDto settings);
    bool AutoPrintEnabled { get; }
    bool AutoPrintHandledByServer { get; }
    string? BarcodePrinter { get; }
    ReceiptPrintOptions? ReceiptOptions { get; set; }
    void PrintReceipt(ReceiptDto receipt);
    void PrintReceipt(ReceiptDto receipt, string printerName, int copies);
    void PrintReceipt(ReceiptDto receipt, string printerName, int copies, ReceiptPrintOptions? options);
    void PrintReturn(CustomerReturnDocumentDto document, string printerName, int copies, ReceiptPrintOptions? options, BusinessDto? business = null);
    void PrintMoneyDocument(MoneyDocument document, bool isPayout, string printerName, int copies, ReceiptPrintOptions? options, BusinessDto? business = null);
    byte[] FormatReturn(CustomerReturnDocumentDto document, ReceiptPrintOptions? options, BusinessDto? business = null);
    void PrintProforma(PreviewDocument document, string? cartCode = null, BusinessDto? business = null, int copies = 1);
    byte[] FormatProforma(PreviewDocument document, string? cartCode, ProformaPrintOptions options, BusinessDto? business = null);
    void PrintZReport(ZReportDto report);
    void PrintZReport(ZReportDto report, string printerName, int copies, string? outputFilePath = null);
    string FormatZReport(ZReportDto report, int? paperWidth = null);
    void PrintRawBytes(string? printerName, byte[] data, string? outputFilePath = null);
    void PrintDocument(string filePath, string? printerName);
    void PrintDocumentImages(IReadOnlyList<byte[]> imagePages);
    void PrintDocumentImages(IReadOnlyList<byte[]> imagePages, string printerName, int copies, string? outputFilePath = null);
}

public sealed class PrinterService : IPrinterService
{
    private readonly AuthService _auth;
    private readonly BranchContextService _branch;
    private readonly string? _path;
    private PrinterSettings _settings = new();
    public event Action? SettingsChanged;

    public PrinterService(AuthService auth, BranchContextService branch)
    {
        _auth = auth;
        _branch = branch;
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cartex");
            Directory.CreateDirectory(dir);
            _path = Path.Combine(dir, "printing.json");
            if (File.Exists(_path))
                _settings = JsonSerializer.Deserialize<PrinterSettings>(File.ReadAllText(_path)) ?? _settings;
            ReceiptOptions = new ReceiptPrintOptions(
                _settings.ReceiptHeaderText,
                _settings.ReceiptFooterText,
                ReceiptPaper.Sanitize(_settings.ReceiptContentWidth),
                _settings.ReceiptShowBusinessName,
                _settings.ReceiptShowBranchName,
                _settings.ReceiptShowAddress,
                _settings.ReceiptShowPhone,
                _settings.ReceiptShowCashier,
                _settings.ReceiptShowCustomer,
                _settings.ReceiptShowNumber,
                _settings.ReceiptShowPaymentDetails,
                _settings.ReceiptShowQrCode,
                _settings.ReceiptShowElectronicLink,
                _settings.ReceiptPublicBaseUrl,
                _settings.ReceiptShowLogo,
                _settings.ReceiptShowCustomerPhone,
                _settings.ReceiptShowCustomerEmail,
                Template: _settings.ReceiptTemplate ?? "auto");
        }
        catch { _path = null; }
    }

    public PrinterSettings GetSettings() => _settings;

    public void SaveSettings(PrinterSettings settings)
    {
        _settings = settings;
        SettingsChanged?.Invoke();
        if (_path is null) return;
        try
        {
            var temporaryPath = $"{_path}.{Guid.NewGuid():N}.tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings));
            File.Move(temporaryPath, _path, true);
        }
        catch { }
    }

    public void CacheBarcodeLabelSettings(BarcodeLabelSettingsDto settings) => SaveSettings(_settings with
    {
        LabelDefaultWithPrice = settings.DefaultWithPrice,
        LabelAllowPriceOverride = settings.AllowPriceOverride,
        LabelShowSku = settings.ShowSku,
        LabelNameLines = settings.NameLines,
        LabelCurrencyDisplay = settings.CurrencyDisplay,
        LabelCurrencyCase = settings.CurrencyCase,
        LabelPriceCurrencyMode = settings.PriceCurrencyMode
    });

    public bool AutoPrintEnabled => _settings.AutoPrintReceipt && ReceiptTarget().Printer is not null;

    /// RUXSAT-04: modul o'chiq bo'lsa server hech narsa chop etmaydi — keshda qolgan eski
    /// bayroq kassani "server chiqaradi" deb aldab, chekni umuman chiqarmay qo'ymasin.
    public bool AutoPrintHandledByServer =>
        _settings.CentralAutoPrint && SettingsService.Instance.IsFeatureOn("remote_printing");

    public string? BarcodePrinter => _settings.BarcodePrinter;
    public ReceiptPrintOptions? ReceiptOptions { get; set; }

    public IReadOnlyList<string> GetInstalledPrinters()
    {
        if (!OperatingSystem.IsWindows()) return [];
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Print\Printers");
            return key?.GetSubKeyNames().OrderBy(n => n).ToArray() ?? [];
        }
        catch { return []; }
    }

    private readonly Dictionary<string, PrinterKind> _kinds = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<string> _installed = [];
    private DateTime _installedAt;

    private IReadOnlyList<string> Installed()
    {
        if (DateTime.UtcNow - _installedAt > TimeSpan.FromSeconds(10))
        {
            _installed = GetInstalledPrinters();
            _installedAt = DateTime.UtcNow;
        }
        return _installed;
    }

    public PrinterKind KindOf(string printerName)
    {
        if (_kinds.TryGetValue(printerName, out var kind)) return kind;
        kind = PrinterClassifier.Classify(printerName, WindowsImagePrinter.MaxPaperWidthMm(printerName));
        _kinds[printerName] = kind;
        return kind;
    }

    private bool Exists(string? printerName) => !string.IsNullOrWhiteSpace(printerName)
        && Installed().Contains(printerName, StringComparer.OrdinalIgnoreCase);

    /// Runs only when the set of installed printers changes, so an explicit user choice -
    /// including a deliberately cleared one - is never overridden while nothing changed.
    public void EnsureAutoSetup()
    {
        var installed = Installed();
        if (installed.Count == 0) return;
        var signature = string.Join("|", installed.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        if (signature == _settings.AutoSetupSignature) return;
        var updated = PrinterAutoSetup.Apply(_settings, installed, KindOf, WindowsImagePrinter.DefaultPrinter())
            with { AutoSetupSignature = signature };
        SaveSettings(updated);
    }

    public PrintTarget ReceiptTarget() => PrintTargetResolver.Receipt(_settings, KindOf, Exists);

    public PrintTarget ProformaTarget(ProformaPrintOptions options) =>
        PrintTargetResolver.Proforma(options, _settings, KindOf, Exists);

    public PrintTarget ZReportTarget() => PrintTargetResolver.ZReport(_settings, KindOf, Exists);

    public PrinterEndpointStatus GetPrinterStatus(string? printerName) => WindowsPrinterHealth.GetStatus(printerName);

    public PrinterCapabilities GetPrinterCapabilities(string? printerName) =>
        new(WindowsImagePrinter.SupportsColor(printerName));

    public DriverPaper? DriverReceiptPaper(string? printerName)
    {
        var mm = WindowsImagePrinter.MaxPaperWidthMm(printerName);
        return ReceiptPaper.ColumnsForMm(mm) is { } columns ? new DriverPaper(mm!.Value, columns) : null;
    }

    /// Kenglik zanjiri: shu kassada aniq tanlangan qiymat -> printer drayveridan aniqlangan
    /// sinf -> biznes/kontent qiymati. "Avto" (0) yangi o'rnatishda hech narsa sozlamasdan
    /// to'g'ri kenglikni beradi.
    private bool ForcedTextOutput =>
        string.Equals(_settings.ReceiptPrintMode, "text", StringComparison.OrdinalIgnoreCase);

    /// Printer kattalashtirishi (`GS !`) butun songa bo'ladi va eng tiniq natijani
    /// beradi; rasm esa kasr nisbatni ham uddalaydi, lekin harf shtrixi qalinlashadi.
    private int TextMagnification => ReceiptPaper.TextMagnification(_settings.ReceiptTextSize);

    private bool NeedsGraphicForTextSize =>
        !ForcedTextOutput && ReceiptPaper.ApplyTextSize(48, _settings.ReceiptTextSize) != 48;

    public int ReceiptWidth(string? printerName, int? contentWidth = null)
    {
        if (ReceiptPaper.IsValid(_settings.ReceiptPaperWidth)) return _settings.ReceiptPaperWidth;
        var nominal = ReceiptNominalWidth(printerName, contentWidth);
        return ReceiptPaper.Sanitize(NeedsGraphicForTextSize
            ? ReceiptPaper.ApplyTextSize(nominal, _settings.ReceiptTextSize)
            : nominal / TextMagnification);
    }

    /// Qog'ozning o'z kengligi — yozuv o'lchami tanlovisiz. Rasm har doim shu kenglikda
    /// chiziladi: aks holda kamroq ustun tanlanganda rasm torayib, qog'ozning chap
    /// tomoniga surilib chiqardi.
    private int ReceiptNominalWidth(string? printerName, int? contentWidth = null) =>
        DriverReceiptPaper(printerName)?.Columns
        ?? (ReceiptPaper.IsValid(_settings.ReceiptPaperWidth)
            ? _settings.ReceiptPaperWidth
            : ReceiptPaper.Sanitize(contentWidth ?? _settings.ReceiptContentWidth));

    public int ReceiptRasterWidth(string? printerName, int? contentWidth = null) =>
        ReceiptPaper.RasterDots(
            _settings.ReceiptRasterWidthDots,
            WindowsImagePrinter.PrintableWidthDots(printerName),
            ReceiptNominalWidth(printerName, contentWidth));

    public void PrintReceipt(ReceiptDto receipt)
    {
        if (string.IsNullOrWhiteSpace(_settings.ReceiptPrinter))
            throw new InvalidOperationException(LocalizationManager.Instance["printer_not_set"]);
        PrintReceipt(receipt, _settings.ReceiptPrinter, _settings.ReceiptCopies);
    }

    public void PrintReceipt(ReceiptDto receipt, string printerName, int copies)
        => PrintReceipt(receipt, printerName, copies, ReceiptOptions);

    public void PrintReceipt(ReceiptDto receipt, string printerName, int copies, ReceiptPrintOptions? options)
    {
        var width = ReceiptWidth(printerName, options?.Width);
        var opts = options is null
            ? new ReceiptPrintOptions(null, null, width)
            : options with { Width = width };
        var document = FormatReceiptDocument(receipt, opts);
        var profile = EscPosProfile.From(_settings);
        // Kasr o'lchamni faqat rasm bera oladi; butun songa kattalashtirish esa
        // printerning o'z shrifti bilan matn rejimida bajariladi.
        var mode = NeedsGraphicForTextSize
            ? "graphic"
            : EscPos.ResolveOutputMode(
                _settings.ReceiptPrintMode, document.Text, profile.Charset, profile.FoldCyrillic);
        var raster = mode == "graphic"
            ? ReceiptRasterRenderer.Render(document, ReceiptRasterWidth(printerName, width), _settings.ReceiptDarkness, _settings.ReceiptQuality)
            : null;
        var bytes = EscPos.BuildDocument(
            document,
            document.QrContent,
            opts.ShowLogo ? opts.LogoRasterBytes : null,
            profile,
            raster);
        for (var i = 0; i < Math.Clamp(copies, 1, 100); i++)
        {
            if (OperatingSystem.IsWindows() && !string.IsNullOrWhiteSpace(printerName))
                RawPrinter.Send(printerName, bytes, "Cartex Receipt", opts?.OutputFilePath);
        }
    }

    public void PrintProforma(PreviewDocument document, string? cartCode = null, BusinessDto? business = null, int copies = 1)
    {
        var options = ProformaPrintOptions.Resolve(_settings);
        var target = ProformaTarget(options);
        if (target.Printer is null)
            throw new InvalidOperationException(LocalizationManager.Instance["printer_not_set"]);
        if (target.IsDocument)
        {
            var pages = ProformaDocumentRenderer.Render(
                document, cartCode is null ? null : $"Savat: {cartCode}", business, options,
                target.Paper, WindowsImagePrinter.SupportsColor(target.Printer));
            WindowsImagePrinter.Print(target.Printer, pages, target.Paper, target.Paper, "portrait", 1,
                Math.Clamp(copies, 1, 100), ProformaPdfPath(target.Printer));
            return;
        }

        if (!ReceiptPaper.IsValid(options.Width))
            options = options with { Width = ReceiptWidth(target.Printer) };
        var bytes = FormatProforma(document, cartCode, options, business);
        for (var i = 0; i < Math.Clamp(copies, 1, 100); i++)
        {
            if (OperatingSystem.IsWindows())
                RawPrinter.Send(target.Printer, bytes, "Cartex Proforma", ProformaPdfPath(target.Printer));
        }
    }

    private string? ProformaPdfPath(string printerName)
    {
        if (!printerName.Contains("Print to PDF", StringComparison.OrdinalIgnoreCase)
            && !printerName.Contains("Save to PDF", StringComparison.OrdinalIgnoreCase)
            && !printerName.Contains("XPS", StringComparison.OrdinalIgnoreCase))
            return null;
        var path = _settings.PdfExportPath;
        if (string.IsNullOrWhiteSpace(path)) return null;
        Directory.CreateDirectory(path);
        return Path.Combine(path, $"Oldindan_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
    }

    public byte[] FormatProforma(PreviewDocument document, string? cartCode, ProformaPrintOptions options, BusinessDto? business = null)
    {
        var w = ReceiptPaper.Sanitize(options.Width);
        options = options with
        {
            HeaderText = string.IsNullOrWhiteSpace(options.HeaderText) ? _settings.ReceiptHeaderText : options.HeaderText,
            FooterText = string.IsNullOrWhiteSpace(options.FooterText) ? _settings.ReceiptFooterText : options.FooterText
        };
        string T(string key) => ReceiptTexts.Get(key, _settings.ReceiptLanguage);
        var sb = new StringBuilder();
        if (options.ShowBusinessName && !string.IsNullOrWhiteSpace(business?.Name)) sb.AppendLine(Center(business.Name, w));
        if (options.ShowAddress && !string.IsNullOrWhiteSpace(business?.Address)) sb.AppendLine(Center(business.Address, w));
        if (options.ShowPhone && !string.IsNullOrWhiteSpace(business?.Phone)) sb.AppendLine(Center(business.Phone, w));
        if (!string.IsNullOrWhiteSpace(options.HeaderText)) sb.AppendLine(Center(options.HeaderText, w));
        sb.AppendLine(new string('=', w));
        sb.AppendLine(Center(T("not_receipt"), w));
        sb.AppendLine(new string('=', w));
        sb.AppendLine(document.CreatedAt.ToString("dd.MM.yyyy HH:mm"));
        if (options.ShowCartCode && !string.IsNullOrWhiteSpace(cartCode)) sb.AppendLine($"{T("cart")}: {cartCode}");
        if (options.ShowSeller && !string.IsNullOrWhiteSpace(document.UserName)) sb.AppendLine($"{T("seller")}: {document.UserName}");
        if (options.ShowCustomer && !string.IsNullOrWhiteSpace(document.CustomerName)) sb.AppendLine($"{T("customer")}: {document.CustomerName}");
        sb.AppendLine(new string('-', w));
        foreach (var line in document.Lines)
        {
            sb.AppendLine(line.Name);
            sb.AppendLine(Row($"  {line.Quantity:0.###} {line.Unit} x {line.UnitPrice:N0}", $"{line.Amount:N0}", w));
        }
        sb.AppendLine(new string('-', w));
        if (document.Discount > 0) sb.AppendLine(Row(T("discount"), $"-{document.Discount:N0}", w));
        sb.AppendLine(Row(T("total"), $"{document.Total:N0}", w));
        if (options.ShowNote && !string.IsNullOrWhiteSpace(document.Note))
        {
            sb.AppendLine(new string('-', w));
            sb.AppendLine($"{T("note")}: {document.Note}");
        }
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(options.FooterText)) sb.AppendLine(Center(options.FooterText, w));
        sb.AppendLine(Center(T("not_receipt"), w));
        sb.AppendLine();
        sb.AppendLine();
        return BuildEscPosReceipt(sb.ToString(), null, null);
    }

    public void PrintReturn(CustomerReturnDocumentDto document, string printerName, int copies, ReceiptPrintOptions? options, BusinessDto? business = null)
    {
        var width = ReceiptWidth(printerName, options?.Width);
        var opts = options is null
            ? new ReceiptPrintOptions(null, null, width)
            : options with { Width = width };
        var bytes = FormatReturn(document, opts, business);
        for (var i = 0; i < Math.Clamp(copies, 1, 100); i++)
        {
            if (OperatingSystem.IsWindows() && !string.IsNullOrWhiteSpace(printerName))
                RawPrinter.Send(printerName, bytes, "Cartex Return", opts?.OutputFilePath);
        }
    }

    public void PrintMoneyDocument(MoneyDocument document, bool isPayout, string printerName, int copies,
        ReceiptPrintOptions? options, BusinessDto? business = null)
    {
        var width = ReceiptWidth(printerName, options?.Width);
        var opts = options is null
            ? new ReceiptPrintOptions(null, null, width)
            : options with { Width = width };
        var bytes = FormatMoneyDocument(document, isPayout, opts, business);
        for (var i = 0; i < Math.Clamp(copies, 1, 100); i++)
        {
            if (OperatingSystem.IsWindows() && !string.IsNullOrWhiteSpace(printerName))
                RawPrinter.Send(printerName, bytes, "Cartex Money", opts?.OutputFilePath);
        }
    }

    public void PrintZReport(ZReportDto r)
    {
        var target = ZReportTarget();
        if (target.Printer is null)
            throw new InvalidOperationException(LocalizationManager.Instance["printer_not_set"]);
        PrintZReport(r, target.Printer, 1);
    }

    public void PrintZReport(ZReportDto r, string printerName, int copies, string? outputFilePath = null)
    {
        if (string.IsNullOrWhiteSpace(printerName)) return;
        // The render style follows the physical printer: a narrow thermal device gets the
        // text layout, everything else gets the document layout.
        if (OperatingSystem.IsWindows() && KindOf(printerName) is not (PrinterKind.ReceiptThermal or PrinterKind.Label))
        {
            var paper = _settings.ZReportDocumentPaperSize == "a5" ? "a5" : "a4";
            var physicalOrientation = _settings.ZReportDocumentOrientation == "landscape"
                ? "landscape"
                : "portrait";
            var pagesPerSheet = _settings.ZReportDocumentPagesPerSheet is 2 or 4
                ? _settings.ZReportDocumentPagesPerSheet
                : 1;
            var pages = ZReportDocumentRenderer.Render(
                r,
                new ZReportDocumentMetadata(
                    _branch.SelectedBranch?.Name ?? LocalizationManager.Instance["z_report"],
                    null,
                    _auth.UserInfo?.FullName ?? _auth.UserInfo?.Username,
                    DateTime.Now),
                paper,
                DocumentPrintLayout.GetReceiptOrientation(physicalOrientation, pagesPerSheet),
                WindowsImagePrinter.SupportsColor(printerName));
            WindowsImagePrinter.Print(printerName, pages, paper, paper, physicalOrientation, pagesPerSheet,
                Math.Clamp(copies, 1, 100), outputFilePath);
            return;
        }

        var width = ReceiptPaper.IsValid(_settings.ZReportPaperWidth)
            ? _settings.ZReportPaperWidth
            : ReceiptWidth(printerName);
        var bytes = BuildEscPosReceipt(FormatZReport(r, width), null);
        for (var i = 0; i < Math.Clamp(copies, 1, 100); i++)
        {
            if (OperatingSystem.IsWindows())
                RawPrinter.Send(printerName, bytes, "Cartex ZReport", outputFilePath);
        }
    }

    public string FormatZReport(ZReportDto r, int? paperWidth = null)
    {
        var configuredWidth = paperWidth
            ?? (ReceiptPaper.IsValid(_settings.ZReportPaperWidth)
                ? _settings.ZReportPaperWidth
                : _settings.ReceiptPaperWidth);
        var w = ReceiptPaper.Sanitize(configuredWidth);
        string T(string key) => LocalizationManager.Instance.Get(_settings.ZReportLanguage, key);
        var sb = new StringBuilder();
        var branchName = _branch.SelectedBranch?.Name;
        var cashierName = _auth.UserInfo?.FullName ?? _auth.UserInfo?.Username;
        if (!string.IsNullOrWhiteSpace(branchName))
            sb.AppendLine(Center(branchName, w));
        sb.AppendLine(Center(T("z_report"), w));
        sb.AppendLine(Center(DateTime.Now.ToString("dd.MM.yyyy HH:mm"), w));
        if (!string.IsNullOrWhiteSpace(cashierName))
            sb.AppendLine(Row(T("cashier"), cashierName, w));
        if (r.ShiftId > 0)
            sb.AppendLine(Row(T("shift"), $"#{r.ShiftId}", w));
        sb.AppendLine(new string('-', w));
        sb.AppendLine(Center(T("z_section_sales"), w));
        sb.AppendLine(Row(T("cash_sales"), $"{r.CashSales:N0}", w));
        if (r.CardSales > 0) sb.AppendLine(Row(T("card_sales"), $"{r.CardSales:N0}", w));
        if (r.BonusUsed > 0) sb.AppendLine(Row(T("bonus_used"), $"{r.BonusUsed:N0}", w));
        if (r.NewDebtIssued > 0) sb.AppendLine(Row(T("debt_issued"), $"{r.NewDebtIssued:N0}", w));
        if (r.CashReturns > 0) sb.AppendLine(Row(T("cash_returns"), $"-{r.CashReturns:N0}", w));
        if (r.CardReturns > 0) sb.AppendLine(Row(T("card_returns"), $"-{r.CardReturns:N0}", w));
        sb.AppendLine(Row(T("sales_count"), $"{r.SalesCount:N0}", w));
        sb.AppendLine(new string('-', w));
        sb.AppendLine(Center(T("z_section_cash"), w));
        sb.AppendLine(Row(T("opening_float"), $"{r.OpeningFloat:N0}", w));
        if (r.PayIn > 0) sb.AppendLine(Row(T("pay_in"), $"{r.PayIn:N0}", w));
        if (r.PayOut > 0) sb.AppendLine(Row(T("pay_out"), $"-{r.PayOut:N0}", w));
        if (r.DebtPayIn > 0) sb.AppendLine(Row(T("debt_pay_in"), $"{r.DebtPayIn:N0}", w));
        if (r.SupplyPayOut > 0) sb.AppendLine(Row(T("supply_pay_out"), $"-{r.SupplyPayOut:N0}", w));
        sb.AppendLine(new string('-', w));
        sb.AppendLine(Center(T("z_section_summary"), w));
        sb.AppendLine(Row(T("expected_cash"), $"{r.ExpectedCash:N0}", w));
        sb.AppendLine(Row(T("counted_cash"), $"{r.CountedCash:N0}", w));
        sb.AppendLine(Row(T("difference"), $"{r.Difference:N0}", w));
        foreach (var c in r.Currencies)
        {
            sb.AppendLine(new string('-', w));
            sb.AppendLine(Center(c.Currency, w));
            sb.AppendLine(Row(T("expected_cash"), $"{c.ExpectedCash:N0}", w));
            sb.AppendLine(Row(T("counted_cash"), $"{c.CountedCash:N0}", w));
            sb.AppendLine(Row(T("difference"), $"{c.Difference:N0}", w));
        }
        sb.AppendLine();
        sb.AppendLine();
        return sb.ToString();
    }

    public void PrintRawBytes(string? printerName, byte[] data, string? outputFilePath = null)
    {
        if (OperatingSystem.IsWindows() && !string.IsNullOrWhiteSpace(printerName))
            RawPrinter.Send(printerName, data, "Cartex Raw Print", outputFilePath);
    }

    public void PrintDocument(string filePath, string? printerName)
    {
        if (!OperatingSystem.IsWindows()) return;
        var psi = string.IsNullOrWhiteSpace(printerName)
            ? new ProcessStartInfo(filePath) { Verb = "print", UseShellExecute = true }
            : new ProcessStartInfo(filePath)
            {
                Verb = "printto",
                Arguments = $"\"{printerName}\"",
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
        Process.Start(psi);
    }

    public void PrintDocumentImages(IReadOnlyList<byte[]> imagePages) =>
        PrintDocumentImages(imagePages, _settings.DocumentPrinter ?? "", _settings.ReceiptCopies, null);

    public void PrintDocumentImages(IReadOnlyList<byte[]> imagePages, string printerName, int copies, string? outputFilePath = null)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(printerName) || imagePages.Count == 0)
            return;

        var physicalPaper = _settings.ReceiptMode is "a4" or "a5"
            ? _settings.ReceiptMode
            : (_settings.DocumentPaperSize is "a5" ? "a5" : "a4");

        WindowsImagePrinter.Print(
            printerName,
            imagePages,
            physicalPaper,
            DocumentPrintLayout.ResolveOutputFormat("document", physicalPaper),
            _settings.DocumentOrientation is "landscape" ? "landscape" : "portrait",
            _settings.DocumentPagesPerSheet is 2 or 4 ? _settings.DocumentPagesPerSheet : 1,
            Math.Clamp(copies, 1, 100),
            outputFilePath);
    }

    public static string FormatReceipt(ReceiptDto receipt, ReceiptPrintOptions? options) =>
        FormatReceiptDocument(receipt, options).Text;

    public static ReceiptTextDocument FormatReceiptDocument(ReceiptDto receipt, ReceiptPrintOptions? options)
    {
        var value = options ?? new ReceiptPrintOptions(null, null, ReceiptPaper.DefaultWidth);
        return ReceiptTextFormatter.Format(receipt, new ReceiptTextOptions(
            value.HeaderText,
            value.FooterText,
            value.Width,
            value.ShowBusinessName,
            value.ShowBranchName,
            value.ShowAddress,
            value.ShowPhone,
            value.ShowCashier,
            value.ShowCustomer,
            value.ShowReceiptNumber,
            value.ShowPaymentDetails,
            value.ShowQrCode,
            value.ShowElectronicLink,
            value.PublicReceiptBaseUrl,
            value.ShowLogo,
            value.ShowCustomerPhone,
            value.ShowCustomerEmail,
            value.Template));
    }

    public byte[] FormatReturn(CustomerReturnDocumentDto document, ReceiptPrintOptions? opts, BusinessDto? business = null)
    {
        var w = ReceiptPaper.Sanitize(opts?.Width ?? 0);
        string T(string key) => ReceiptTexts.Get(key, _settings.ReceiptLanguage);
        var sb = new StringBuilder();
        var branchPhone = _branch.SelectedBranch?.Phone;
        var phone = string.IsNullOrWhiteSpace(branchPhone)
            ? business?.Phone
            : branchPhone;
        if (opts?.ShowBusinessName != false && !string.IsNullOrWhiteSpace(business?.Name)) sb.AppendLine(Center(business.Name, w));
        if (opts?.ShowAddress != false && !string.IsNullOrWhiteSpace(business?.Address)) sb.AppendLine(Center(business.Address, w));
        if (opts?.ShowPhone != false && !string.IsNullOrWhiteSpace(phone)) sb.AppendLine(Center(phone, w));
        if (!string.IsNullOrWhiteSpace(opts?.HeaderText)) sb.AppendLine(Center(opts.HeaderText, w));
        sb.AppendLine(Center(T("return_title"), w));
        sb.AppendLine(new string('=', w));
        sb.AppendLine(document.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm"));
        sb.AppendLine($"{T("document_no")} {document.DocumentNumber}");
        sb.AppendLine($"{T("warehouse")}: {document.WarehouseName}");
        if (opts?.ShowCashier != false && !string.IsNullOrWhiteSpace(document.UserName)) sb.AppendLine($"{T("staff")}: {document.UserName}");
        if (opts?.ShowCustomer != false && !string.IsNullOrWhiteSpace(document.CustomerName)) sb.AppendLine($"{T("customer")}: {document.CustomerName}");
        sb.AppendLine(new string('-', w));
        foreach (var line in document.Lines)
        {
            sb.AppendLine(line.ProductName);
            sb.AppendLine(Row($"  {line.Quantity:0.###} {line.UnitName} x {line.UnitPrice:N0}", $"{line.LineAmount:N0}", w));
        }
        sb.AppendLine(new string('-', w));
        sb.AppendLine(Row(T("total"), $"{document.RefundAmount:N0}", w));
        foreach (var settlement in document.Settlements)
            sb.AppendLine(Row($"  {SettlementLabel(settlement.Method, _settings.ReceiptLanguage)}", $"{settlement.AmountBase:N0}", w));
        if (!string.IsNullOrWhiteSpace(document.Note))
        {
            sb.AppendLine(new string('-', w));
            sb.AppendLine($"{T("note")}: {document.Note}");
        }
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(opts?.FooterText)) sb.AppendLine(Center(opts.FooterText, w));
        sb.AppendLine();
        sb.AppendLine();
        return BuildEscPosReceipt(sb.ToString(), null, opts?.LogoRasterBytes);
    }

    public byte[] FormatMoneyDocument(MoneyDocument doc, bool isPayout, ReceiptPrintOptions? opts, BusinessDto? business = null)
    {
        var w = ReceiptPaper.Sanitize(opts?.Width ?? 0);
        string T(string key) => ReceiptTexts.Get(key, _settings.ReceiptLanguage);
        var sb = new StringBuilder();
        var branchPhone = _branch.SelectedBranch?.Phone;
        var phone = string.IsNullOrWhiteSpace(branchPhone)
            ? business?.Phone
            : branchPhone;
        if (opts?.ShowBusinessName != false && !string.IsNullOrWhiteSpace(business?.Name)) sb.AppendLine(Center(business.Name, w));
        if (opts?.ShowAddress != false && !string.IsNullOrWhiteSpace(business?.Address)) sb.AppendLine(Center(business.Address, w));
        if (opts?.ShowPhone != false && !string.IsNullOrWhiteSpace(phone)) sb.AppendLine(Center(phone, w));
        if (!string.IsNullOrWhiteSpace(opts?.HeaderText)) sb.AppendLine(Center(opts.HeaderText, w));
        sb.AppendLine(Center(T(isPayout ? "payout_title" : "payment_title"), w));
        sb.AppendLine(new string('=', w));
        sb.AppendLine(doc.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm"));
        sb.AppendLine($"{T("document_no")} {doc.Number}");
        if (opts?.ShowCashier != false && !string.IsNullOrWhiteSpace(doc.UserName)) sb.AppendLine($"{T("staff")}: {doc.UserName}");
        if (!string.IsNullOrWhiteSpace(doc.CustomerName)) sb.AppendLine($"{T("customer")}: {doc.CustomerName}");
        sb.AppendLine(new string('-', w));

        foreach (var tender in doc.Tenders)
            sb.AppendLine(Row(SettlementLabel(tender.Method, _settings.ReceiptLanguage), $"{tender.AmountBase:N0}", w));

        sb.AppendLine(new string('-', w));
        sb.AppendLine(Row(T(isPayout ? "given" : "received"), $"{doc.TotalBase:N0}", w));

        // The customer's copy has to say what the money did, not just how much moved.
        if (doc.AdvanceBase > 0) sb.AppendLine(Row($"  {T(isPayout ? "advance" : "to_advance")}", $"{doc.AdvanceBase:N0}", w));
        if (doc.LoanBase > 0) sb.AppendLine(Row($"  {T("loan_given")}", $"{doc.LoanBase:N0}", w));
        if (doc.WriteOffBase > 0) sb.AppendLine(Row($"  {T("forgiven")}", $"{doc.WriteOffBase:N0}", w));

        sb.AppendLine(new string('=', w));
        sb.AppendLine(Row(T(doc.BalanceAfter >= 0 ? "debt_balance" : "advance_balance"), $"{Math.Abs(doc.BalanceAfter):N0}", w));

        if (!string.IsNullOrWhiteSpace(doc.Note))
        {
            sb.AppendLine(new string('-', w));
            sb.AppendLine($"{T("note")}: {doc.Note}");
        }
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(opts?.FooterText)) sb.AppendLine(Center(opts.FooterText, w));
        sb.AppendLine();
        sb.AppendLine();
        return BuildEscPosReceipt(sb.ToString(), null, opts?.LogoRasterBytes);
    }

    private static string SettlementLabel(string method, string? language) => method switch
    {
        "ReduceDebt" => ReceiptTexts.Get("from_debt", language),
        "Cash" => ReceiptTexts.Get("cash", language),
        "Card" => ReceiptTexts.Get("card", language),
        "Bonus" => ReceiptTexts.Get("bonus", language),
        "CustomerAdvance" => ReceiptTexts.Get("to_advance", language),
        "NoCharge" => ReceiptTexts.Get("no_charge", language),
        _ => method
    };

    private byte[] BuildEscPosReceipt(string text, string? qrContent, byte[]? logoRasterBytes = null)
    {
        var profile = EscPosProfile.From(_settings);
        var width = ReceiptPaper.Sanitize(_settings.ReceiptContentWidth);
        var document = ReceiptTextDocument.Plain(text, width);
        var mode = EscPos.ResolveOutputMode(_settings.ReceiptPrintMode, text, profile.Charset);
        var raster = mode == "graphic"
            ? ReceiptRasterRenderer.Render(document, ReceiptRasterWidth(_settings.ReceiptPrinter, width), _settings.ReceiptDarkness, _settings.ReceiptQuality)
            : null;
        return EscPos.BuildDocument(document, qrContent, logoRasterBytes, profile, raster);
    }

    private static string Center(string s, int w)
    {
        s = s.Length > w ? s[..w] : s;
        var pad = (w - s.Length) / 2;
        return new string(' ', Math.Max(0, pad)) + s;
    }

    private static string Row(string left, string right, int w)
    {
        if (right.Length >= w)
            return right[..w];

        var maxLeftLength = w - right.Length - 1;
        if (left.Length > maxLeftLength)
            left = left[..maxLeftLength];

        return left + new string(' ', w - left.Length - right.Length) + right;
    }
}

internal static class WindowsPrinterHealth
{
    private const uint PrinterStatusError = 0x00000002;
    private const uint PrinterStatusPaperJam = 0x00000008;
    private const uint PrinterStatusPaperOut = 0x00000010;
    private const uint PrinterStatusPaperProblem = 0x00000040;
    private const uint PrinterStatusOffline = 0x00000080;
    private const uint PrinterStatusBusy = 0x00000200;
    private const uint PrinterStatusPrinting = 0x00000400;
    private const uint PrinterStatusOutputBinFull = 0x00000800;
    private const uint PrinterStatusNotAvailable = 0x00001000;
    private const uint PrinterStatusProcessing = 0x00004000;
    private const uint PrinterStatusNoToner = 0x00040000;
    private const uint PrinterStatusUserIntervention = 0x00100000;
    private const uint PrinterStatusDoorOpen = 0x00400000;
    private const uint PrinterAttributeWorkOffline = 0x00000400;

    [StructLayout(LayoutKind.Sequential)]
    private struct PrinterInfo2
    {
        public IntPtr ServerName;
        public IntPtr PrinterName;
        public IntPtr ShareName;
        public IntPtr PortName;
        public IntPtr DriverName;
        public IntPtr Comment;
        public IntPtr Location;
        public IntPtr DevMode;
        public IntPtr SeparatorFile;
        public IntPtr PrintProcessor;
        public IntPtr DataType;
        public IntPtr Parameters;
        public IntPtr SecurityDescriptor;
        public uint Attributes;
        public uint Priority;
        public uint DefaultPriority;
        public uint StartTime;
        public uint UntilTime;
        public uint Status;
        public uint Jobs;
        public uint AveragePagesPerMinute;
    }

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool OpenPrinter(string printerName, out IntPtr printer, IntPtr defaults);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool GetPrinter(IntPtr printer, uint level, IntPtr buffer, uint size, out uint needed);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr printer);

    public static PrinterEndpointStatus GetStatus(string? printerName)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(printerName))
            return PrinterEndpointStatus.Offline;
        if (!OpenPrinter(printerName, out var printer, IntPtr.Zero))
            return PrinterEndpointStatus.Offline;
        try
        {
            GetPrinter(printer, 2, IntPtr.Zero, 0, out var needed);
            if (needed == 0) return PrinterEndpointStatus.Error;
            var buffer = Marshal.AllocHGlobal((int)needed);
            try
            {
                if (!GetPrinter(printer, 2, buffer, needed, out _))
                    return PrinterEndpointStatus.Error;
                var info = Marshal.PtrToStructure<PrinterInfo2>(buffer);
                if ((info.Attributes & PrinterAttributeWorkOffline) != 0
                    || (info.Status & (PrinterStatusOffline | PrinterStatusNotAvailable)) != 0)
                    return PrinterEndpointStatus.Offline;
                if ((info.Status & (PrinterStatusError | PrinterStatusPaperJam | PrinterStatusPaperOut
                    | PrinterStatusPaperProblem | PrinterStatusOutputBinFull | PrinterStatusNoToner
                    | PrinterStatusUserIntervention | PrinterStatusDoorOpen)) != 0)
                    return PrinterEndpointStatus.Error;
                if ((info.Status & (PrinterStatusBusy | PrinterStatusPrinting | PrinterStatusProcessing)) != 0)
                    return PrinterEndpointStatus.Busy;
                return PrinterEndpointStatus.Ready;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        finally
        {
            ClosePrinter(printer);
        }
    }
}

internal static class RawPrinter
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DOCINFO
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string DocName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? OutputFile;
        [MarshalAs(UnmanagedType.LPWStr)] public string DataType;
    }

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool OpenPrinter(string src, out IntPtr hPrinter, IntPtr pd);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool StartDocPrinter(IntPtr hPrinter, int level, ref DOCINFO di);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr hPrinter, IntPtr buf, int count, out int written);

    public static void Send(string printerName, byte[] bytes, string docName, string? outputFilePath = null)
    {
        if (!OperatingSystem.IsWindows()) return;
        var unmanaged = Marshal.AllocCoTaskMem(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, unmanaged, bytes.Length);
            if (!OpenPrinter(printerName, out var hPrinter, IntPtr.Zero))
                throw new InvalidOperationException($"OpenPrinter '{printerName}' failed (Win32 error {Marshal.GetLastWin32Error()})");
            try
            {
                var di = new DOCINFO { DocName = docName, DataType = "RAW", OutputFile = outputFilePath };
                if (!StartDocPrinter(hPrinter, 1, ref di))
                    throw new InvalidOperationException($"StartDocPrinter '{printerName}' failed (Win32 error {Marshal.GetLastWin32Error()})");
                try
                {
                    if (!StartPagePrinter(hPrinter))
                        throw new InvalidOperationException($"StartPagePrinter '{printerName}' failed (Win32 error {Marshal.GetLastWin32Error()})");
                    if (!WritePrinter(hPrinter, unmanaged, bytes.Length, out var written) || written != bytes.Length)
                        throw new InvalidOperationException($"WritePrinter '{printerName}' failed (Win32 error {Marshal.GetLastWin32Error()})");
                    if (!EndPagePrinter(hPrinter))
                        throw new InvalidOperationException($"EndPagePrinter '{printerName}' failed (Win32 error {Marshal.GetLastWin32Error()})");
                }
                finally { EndDocPrinter(hPrinter); }
            }
            finally { ClosePrinter(hPrinter); }
        }
        finally { Marshal.FreeCoTaskMem(unmanaged); }
    }
}
