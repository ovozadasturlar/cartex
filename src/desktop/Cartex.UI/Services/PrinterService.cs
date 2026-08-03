using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Cartex.Shared.Localization;
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
    public bool AutoPrintReceipt { get; init; }
    public double LabelWidthMm { get; init; }
    public double LabelHeightMm { get; init; }
    public string? ReceiptMode { get; init; }
    public int ReceiptPaperWidth { get; init; }
    public int ReceiptCopies { get; init; } = 1;
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
    public int LabelNameLines { get; init; } = 2;
    public bool LabelDefaultWithPrice { get; init; }
    public bool LabelShowSku { get; init; }
    public string? DocumentPaperSize { get; init; }
    public string? DocumentOrientation { get; init; }
    public int DocumentPagesPerSheet { get; init; } = 1;
    public string? ZReportMode { get; init; }
    public int ZReportPaperWidth { get; init; }
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
    string? PublicReceiptBaseUrl = null);

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

public interface IPrinterService
{
    IReadOnlyList<string> GetInstalledPrinters();
    PrinterCapabilities GetPrinterCapabilities(string? printerName);
    PrinterSettings GetSettings();
    void SaveSettings(PrinterSettings settings);
    bool AutoPrintEnabled { get; }
    string? BarcodePrinter { get; }
    ReceiptPrintOptions? ReceiptOptions { get; set; }
    void PrintReceipt(ReceiptDto receipt);
    void PrintReceipt(ReceiptDto receipt, string printerName, int copies);
    void PrintZReport(ZReportDto report);
    void PrintZReport(ZReportDto report, string printerName, int copies);
    string FormatZReport(ZReportDto report, int? paperWidth = null);
    void PrintRaw(string? printerName, string text);
    void PrintRawBytes(string? printerName, byte[] data);
    void PrintDocument(string filePath, string? printerName);
    void PrintDocumentImages(IReadOnlyList<byte[]> imagePages);
    void PrintDocumentImages(IReadOnlyList<byte[]> imagePages, string printerName, int copies);
}

public sealed class PrinterService : IPrinterService
{
    private readonly AuthService _auth;
    private readonly BranchContextService _branch;
    private readonly string? _path;
    private PrinterSettings _settings = new();

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
        }
        catch { _path = null; }
    }

    public PrinterSettings GetSettings() => _settings;

    public void SaveSettings(PrinterSettings settings)
    {
        _settings = settings;
        if (_path is null) return;
        try { File.WriteAllText(_path, JsonSerializer.Serialize(settings)); }
        catch { }
    }

    public bool AutoPrintEnabled =>
        _settings.AutoPrintReceipt &&
        (_settings.ReceiptMode is "a4" or "a5"
            ? !string.IsNullOrWhiteSpace(_settings.DocumentPrinter)
            : !string.IsNullOrWhiteSpace(_settings.ReceiptPrinter));

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

    public PrinterCapabilities GetPrinterCapabilities(string? printerName) =>
        new(WindowsImagePrinter.SupportsColor(printerName));

    public void PrintReceipt(ReceiptDto receipt) =>
        PrintReceipt(receipt, _settings.ReceiptPrinter ?? "", _settings.ReceiptCopies);

    public void PrintReceipt(ReceiptDto receipt, string printerName, int copies)
    {
        var opts = _settings.ReceiptPaperWidth is 32 or 42 or 48
            ? ReceiptOptions is null
                ? new ReceiptPrintOptions(null, null, _settings.ReceiptPaperWidth)
                : ReceiptOptions with { Width = _settings.ReceiptPaperWidth }
            : ReceiptOptions;
        var text = FormatReceipt(receipt, opts);
        var link = opts?.ShowQrCode != false && !string.IsNullOrWhiteSpace(opts?.PublicReceiptBaseUrl)
            ? $"{opts.PublicReceiptBaseUrl.TrimEnd('/')}/r/{receipt.ReceiptToken}"
            : null;
        for (var i = 0; i < Math.Clamp(copies, 1, 100); i++)
        {
            if (link is null)
                PrintRaw(printerName, text);
            else if (OperatingSystem.IsWindows() && !string.IsNullOrWhiteSpace(printerName))
                RawPrinter.Send(printerName, BuildEscPosReceipt(text, link), "Cartex Receipt");
        }
    }

    public void PrintZReport(ZReportDto r)
    {
        var configured = string.IsNullOrWhiteSpace(_settings.ZReportPrinter)
            ? (_settings.ZReportMode is "a4" or "a5" ? _settings.DocumentPrinter : _settings.ReceiptPrinter)
            : _settings.ZReportPrinter;
        PrintZReport(r, configured ?? "", 1);
    }

    public void PrintZReport(ZReportDto r, string printerName, int copies)
    {
        var mode = DocumentPrintLayout.ResolveOutputFormat(
            _settings.ZReportMode is "a4" or "a5" ? "document" : "thermal",
            _settings.ZReportDocumentPaperSize ?? "a4");
        if (mode == "thermal")
        {
            var width = _settings.ZReportPaperWidth is 32 or 42 or 48
                ? _settings.ZReportPaperWidth
                : _settings.ReceiptPaperWidth;
            for (var i = 0; i < Math.Clamp(copies, 1, 100); i++)
                PrintRaw(printerName, FormatZReport(r, width));
            return;
        }

        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(printerName))
            return;

        var physicalPaper = _settings.ZReportDocumentPaperSize == "a5" ? "a5" : "a4";
        var physicalOrientation = _settings.ZReportDocumentOrientation == "landscape"
            ? "landscape"
            : "portrait";
        var pagesPerSheet = _settings.ZReportDocumentPagesPerSheet is 2 or 4
            ? _settings.ZReportDocumentPagesPerSheet
            : 1;
        var reportOrientation = DocumentPrintLayout.GetReceiptOrientation(
            physicalOrientation,
            pagesPerSheet);
        var supportsColor = WindowsImagePrinter.SupportsColor(printerName);
        var pages = ZReportDocumentRenderer.Render(
            r,
            new ZReportDocumentMetadata(
                _branch.SelectedBranch?.Name ?? LocalizationManager.Instance["z_report"],
                null,
                _auth.UserInfo?.FullName ?? _auth.UserInfo?.Username,
                DateTime.Now),
            mode,
            reportOrientation,
            supportsColor);

        WindowsImagePrinter.Print(
            printerName,
            pages,
            physicalPaper,
            mode,
            physicalOrientation,
            pagesPerSheet,
            Math.Clamp(copies, 1, 100));
    }

    public string FormatZReport(ZReportDto r, int? paperWidth = null)
    {
        var configuredWidth = paperWidth
            ?? (_settings.ZReportPaperWidth is 32 or 42 or 48
                ? _settings.ZReportPaperWidth
                : _settings.ReceiptPaperWidth);
        var w = Math.Clamp(configuredWidth <= 0 ? 32 : configuredWidth, 24, 120);
        var l = LocalizationManager.Instance;
        var sb = new StringBuilder();
        var branchName = _branch.SelectedBranch?.Name;
        var cashierName = _auth.UserInfo?.FullName ?? _auth.UserInfo?.Username;
        if (!string.IsNullOrWhiteSpace(branchName))
            sb.AppendLine(Center(branchName, w));
        sb.AppendLine(Center(l["z_report"], w));
        sb.AppendLine(Center(DateTime.Now.ToString("dd.MM.yyyy HH:mm"), w));
        if (!string.IsNullOrWhiteSpace(cashierName))
            sb.AppendLine(Row(l["cashier"], cashierName, w));
        if (r.ShiftId > 0)
            sb.AppendLine(Row(l["shift"], $"#{r.ShiftId}", w));
        sb.AppendLine(new string('-', w));
        sb.AppendLine(Center(l["z_section_sales"], w));
        sb.AppendLine(Row(l["cash_sales"], $"{r.CashSales:N0}", w));
        if (r.CardSales > 0) sb.AppendLine(Row(l["card_sales"], $"{r.CardSales:N0}", w));
        if (r.BonusUsed > 0) sb.AppendLine(Row(l["bonus_used"], $"{r.BonusUsed:N0}", w));
        if (r.NewDebtIssued > 0) sb.AppendLine(Row(l["debt_issued"], $"{r.NewDebtIssued:N0}", w));
        if (r.CashReturns > 0) sb.AppendLine(Row(l["cash_returns"], $"-{r.CashReturns:N0}", w));
        if (r.CardReturns > 0) sb.AppendLine(Row(l["card_returns"], $"-{r.CardReturns:N0}", w));
        sb.AppendLine(Row(l["sales_count"], $"{r.SalesCount:N0}", w));
        sb.AppendLine(new string('-', w));
        sb.AppendLine(Center(l["z_section_cash"], w));
        sb.AppendLine(Row(l["opening_float"], $"{r.OpeningFloat:N0}", w));
        if (r.PayIn > 0) sb.AppendLine(Row(l["pay_in"], $"{r.PayIn:N0}", w));
        if (r.PayOut > 0) sb.AppendLine(Row(l["pay_out"], $"-{r.PayOut:N0}", w));
        if (r.DebtPayIn > 0) sb.AppendLine(Row(l["debt_pay_in"], $"{r.DebtPayIn:N0}", w));
        if (r.SupplyPayOut > 0) sb.AppendLine(Row(l["supply_pay_out"], $"-{r.SupplyPayOut:N0}", w));
        sb.AppendLine(new string('-', w));
        sb.AppendLine(Center(l["z_section_summary"], w));
        sb.AppendLine(Row(l["expected_cash"], $"{r.ExpectedCash:N0}", w));
        sb.AppendLine(Row(l["counted_cash"], $"{r.CountedCash:N0}", w));
        sb.AppendLine(Row(l["difference"], $"{r.Difference:N0}", w));
        foreach (var c in r.Currencies)
        {
            sb.AppendLine(new string('-', w));
            sb.AppendLine(Center(c.Currency, w));
            sb.AppendLine(Row(l["expected_cash"], $"{c.ExpectedCash:N0}", w));
            sb.AppendLine(Row(l["counted_cash"], $"{c.CountedCash:N0}", w));
            sb.AppendLine(Row(l["difference"], $"{c.Difference:N0}", w));
        }
        sb.AppendLine();
        sb.AppendLine();
        return sb.ToString();
    }

    public void PrintRaw(string? printerName, string text)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(printerName)) return;
        RawPrinter.Send(printerName, Encoding.UTF8.GetBytes(text), "Cartex Receipt");
    }

    public void PrintRawBytes(string? printerName, byte[] data)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(printerName) || data.Length == 0) return;
        RawPrinter.Send(printerName, data, "Cartex Label");
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
        PrintDocumentImages(imagePages, _settings.DocumentPrinter ?? "", _settings.ReceiptCopies);

    public void PrintDocumentImages(IReadOnlyList<byte[]> imagePages, string printerName, int copies)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(printerName) || imagePages.Count == 0)
            return;

        WindowsImagePrinter.Print(
            printerName,
            imagePages,
            _settings.DocumentPaperSize is "a5" ? "a5" : "a4",
            DocumentPrintLayout.ResolveOutputFormat("document", _settings.DocumentPaperSize ?? "a4"),
            _settings.DocumentOrientation is "landscape" ? "landscape" : "portrait",
            _settings.DocumentPagesPerSheet is 2 or 4 ? _settings.DocumentPagesPerSheet : 1,
            Math.Clamp(copies, 1, 100));
    }

    private static string FormatReceipt(ReceiptDto r, ReceiptPrintOptions? opts)
    {
        var w = opts?.Width is 42 or 48 ? opts.Width : 32;
        string T(string key) => ReceiptTexts.Get(key, r.Language);
        var sb = new StringBuilder();
        if (opts?.ShowBusinessName != false) sb.AppendLine(Center(r.BusinessName, w));
        if (opts?.ShowBranchName != false && !string.IsNullOrWhiteSpace(r.BranchName)) sb.AppendLine(Center(r.BranchName, w));
        if (opts?.ShowAddress != false && !string.IsNullOrWhiteSpace(r.BranchAddress)) sb.AppendLine(Center(r.BranchAddress!, w));
        if (opts?.ShowPhone != false && !string.IsNullOrWhiteSpace(r.BranchPhone)) sb.AppendLine(Center(r.BranchPhone!, w));
        if (!string.IsNullOrWhiteSpace(opts?.HeaderText)) sb.AppendLine(Center(opts.HeaderText, w));
        sb.AppendLine(r.SaleDate.ToString("dd.MM.yyyy HH:mm"));
        if (opts?.ShowReceiptNumber != false) sb.AppendLine($"{T("receipt_no")} {r.SaleId}");
        if (opts?.ShowCashier != false && !string.IsNullOrWhiteSpace(r.UserName)) sb.AppendLine($"{T("cashier")}: {r.UserName}");
        if (opts?.ShowCustomer != false && !string.IsNullOrWhiteSpace(r.CustomerName)) sb.AppendLine($"{T("customer")}: {r.CustomerName}");
        sb.AppendLine(new string('-', w));
        foreach (var i in r.Items)
        {
            sb.AppendLine(i.ProductName);
            sb.AppendLine(Row($"  {i.Quantity:0.###} x {i.UnitPrice:N0}", $"{i.LineTotal:N0}", w));
        }
        sb.AppendLine(new string('-', w));
        if (r.DiscountAmount > 0) sb.AppendLine(Row(T("discount"), $"{r.DiscountAmount:N0}", w));
        sb.AppendLine(Row(T("total"), $"{r.TotalAmount:N0}", w));
        if (opts?.ShowPaymentDetails != false)
        {
            if (r.Payments.Count > 0)
                foreach (var p in r.Payments)
                    sb.AppendLine(Row(ReceiptTexts.PaymentLabel(p.Method, r.Language), p.IsForeign ? $"{p.Amount:N2} {p.Currency} ≈ {p.AmountBase:N0}" : $"{p.Amount:N0}", w));
            else
            {
                if (r.PaidCash > 0) sb.AppendLine(Row(T("cash"), $"{r.PaidCash:N0}", w));
                if (r.PaidCard > 0) sb.AppendLine(Row(T("card"), $"{r.PaidCard:N0}", w));
                if (r.PaidBonus > 0) sb.AppendLine(Row(T("bonus"), $"{r.PaidBonus:N0}", w));
            }
            if (r.ChangeAmount > 0) sb.AppendLine(Row(T("change"), $"{r.ChangeAmount:N0}", w));
            if (r.CreditAmount > 0) sb.AppendLine(Row(T("credit"), $"{r.CreditAmount:N0}", w));
            if (r.DebtAmount > 0) sb.AppendLine(Row(T("debt"), $"{r.DebtAmount:N0}", w));
            if (r.CashbackEarned > 0) sb.AppendLine(Row(T("cashback"), $"{r.CashbackEarned:N0}", w));
        }
        sb.AppendLine(new string('-', w));
        sb.AppendLine(Center(T(r.DebtAmount > 0 ? "unpaid" : "paid"), w));
        sb.AppendLine();
        sb.AppendLine(Center(string.IsNullOrWhiteSpace(opts?.FooterText) ? T("thanks") : opts.FooterText, w));
        if (opts?.ShowElectronicLink != false && !string.IsNullOrWhiteSpace(opts?.PublicReceiptBaseUrl))
            sb.AppendLine($"{opts.PublicReceiptBaseUrl.TrimEnd('/')}/r/{r.ReceiptToken}");
        sb.AppendLine();
        sb.AppendLine();
        return sb.ToString();
    }

    private static byte[] BuildEscPosReceipt(string text, string qrContent)
    {
        var output = new List<byte>(Encoding.UTF8.GetByteCount(text) + qrContent.Length + 64);
        output.AddRange(Encoding.UTF8.GetBytes(text));

        static void AddCommand(List<byte> bytes, params byte[] command) => bytes.AddRange(command);
        AddCommand(output, 0x1D, 0x28, 0x6B, 0x04, 0x00, 0x31, 0x41, 0x32, 0x00);
        AddCommand(output, 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x43, 0x05);
        AddCommand(output, 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x45, 0x31);

        var data = Encoding.UTF8.GetBytes(qrContent);
        var length = data.Length + 3;
        AddCommand(output, 0x1D, 0x28, 0x6B, (byte)(length & 0xFF), (byte)(length >> 8), 0x31, 0x50, 0x30);
        output.AddRange(data);
        AddCommand(output, 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x51, 0x30, 0x0A, 0x0A);
        return output.ToArray();
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

    public static void Send(string printerName, byte[] bytes, string docName)
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
                var di = new DOCINFO { DocName = docName, DataType = "RAW" };
                if (!StartDocPrinter(hPrinter, 1, ref di))
                    throw new InvalidOperationException($"StartDocPrinter '{printerName}' failed (Win32 error {Marshal.GetLastWin32Error()})");
                try
                {
                    if (!StartPagePrinter(hPrinter)) return;
                    WritePrinter(hPrinter, unmanaged, bytes.Length, out _);
                    EndPagePrinter(hPrinter);
                }
                finally { EndDocPrinter(hPrinter); }
            }
            finally { ClosePrinter(hPrinter); }
        }
        finally { Marshal.FreeCoTaskMem(unmanaged); }
    }
}
