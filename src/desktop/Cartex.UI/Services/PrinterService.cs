using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Cartex.Shared.Localization;
using Cartex.Shared.Models.Sales;
using Cartex.Shared.Models.Shifts;

namespace Cartex.UI.Services;

public record PrinterSettings(string? ReceiptPrinter, string? ZReportPrinter, string? BarcodePrinter, string? DocumentPrinter, bool AutoPrintReceipt, double LabelWidthMm = 0, double LabelHeightMm = 0, string? ReceiptMode = null, int ReceiptPaperWidth = 0, int ReceiptCopies = 1, bool AutoPrintZReport = false, string? LabelMode = null, double LabelGapMm = 0, int LabelDpi = 0, double LabelShiftXMm = 0, double LabelShiftYMm = 0, int LabelRotation = -1, int LabelDensity = 0, int LabelSpeed = 0, bool UsePrinterGapCalibration = false);

public record ReceiptPrintOptions(string? HeaderText, string? FooterText, int Width);

public record LabelOptions(double WidthMm, double HeightMm, double GapMm, int Dpi, double ShiftXMm, double ShiftYMm, int Rotation, int Density, int Speed, bool UsePrinterGapCalibration);

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
            s.UsePrinterGapCalibration);
    }
}

public interface IPrinterService
{
    IReadOnlyList<string> GetInstalledPrinters();
    PrinterSettings GetSettings();
    void SaveSettings(PrinterSettings settings);
    bool AutoPrintEnabled { get; }
    string? BarcodePrinter { get; }
    ReceiptPrintOptions? ReceiptOptions { get; set; }
    void PrintReceipt(ReceiptDto receipt);
    void PrintZReport(ZReportDto report);
    void PrintRaw(string? printerName, string text);
    void PrintRawBytes(string? printerName, byte[] data);
    void PrintDocument(string filePath, string? printerName);
}

public sealed class PrinterService : IPrinterService
{
    private readonly string? _path;
    private PrinterSettings _settings = new(null, null, null, null, false);

    public PrinterService()
    {
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

    public bool AutoPrintEnabled => _settings.AutoPrintReceipt && !string.IsNullOrWhiteSpace(_settings.ReceiptPrinter);

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

    public void PrintReceipt(ReceiptDto receipt)
    {
        var opts = _settings.ReceiptPaperWidth is 32 or 42 or 48
            ? new ReceiptPrintOptions(ReceiptOptions?.HeaderText, ReceiptOptions?.FooterText, _settings.ReceiptPaperWidth)
            : ReceiptOptions;
        var text = FormatReceipt(receipt, opts);
        for (var i = 0; i < Math.Clamp(_settings.ReceiptCopies, 1, 5); i++)
            PrintRaw(_settings.ReceiptPrinter, text);
    }

    public void PrintZReport(ZReportDto r)
    {
        var w = _settings.ReceiptPaperWidth is 42 or 48 ? _settings.ReceiptPaperWidth : 32;
        var l = LocalizationManager.Instance;
        var sb = new StringBuilder();
        sb.AppendLine(Center(l["z_report"], w));
        sb.AppendLine(Center(DateTime.Now.ToString("dd.MM.yyyy HH:mm"), w));
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
        PrintRaw(string.IsNullOrWhiteSpace(_settings.ZReportPrinter) ? _settings.ReceiptPrinter : _settings.ZReportPrinter, sb.ToString());
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
        try
        {
            var psi = string.IsNullOrWhiteSpace(printerName)
                ? new ProcessStartInfo(filePath) { Verb = "print", UseShellExecute = true }
                : new ProcessStartInfo(filePath) { Verb = "printto", Arguments = $"\"{printerName}\"", UseShellExecute = true, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
            Process.Start(psi);
        }
        catch
        {
            try { Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true }); } catch { }
        }
    }

    private static string FormatReceipt(ReceiptDto r, ReceiptPrintOptions? opts)
    {
        var w = opts?.Width is 42 or 48 ? opts.Width : 32;
        string T(string key) => ReceiptTexts.Get(key, r.Language);
        var sb = new StringBuilder();
        sb.AppendLine(Center(r.BusinessName, w));
        if (!string.IsNullOrWhiteSpace(r.BranchName)) sb.AppendLine(Center(r.BranchName, w));
        if (!string.IsNullOrWhiteSpace(r.BranchAddress)) sb.AppendLine(Center(r.BranchAddress!, w));
        if (!string.IsNullOrWhiteSpace(r.BranchPhone)) sb.AppendLine(Center(r.BranchPhone!, w));
        if (!string.IsNullOrWhiteSpace(opts?.HeaderText)) sb.AppendLine(Center(opts.HeaderText, w));
        sb.AppendLine(r.SaleDate.ToString("dd.MM.yyyy HH:mm"));
        if (!string.IsNullOrWhiteSpace(r.UserName)) sb.AppendLine($"{T("cashier")}: {r.UserName}");
        sb.AppendLine(new string('-', w));
        foreach (var i in r.Items)
        {
            sb.AppendLine(i.ProductName);
            sb.AppendLine(Row($"  {i.Quantity:0.###} x {i.UnitPrice:N0}", $"{i.LineTotal:N0}", w));
        }
        sb.AppendLine(new string('-', w));
        if (r.DiscountAmount > 0) sb.AppendLine(Row(T("discount"), $"{r.DiscountAmount:N0}", w));
        sb.AppendLine(Row(T("total"), $"{r.TotalAmount:N0}", w));
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
        sb.AppendLine(new string('-', w));
        sb.AppendLine(Center(T(r.DebtAmount > 0 ? "unpaid" : "paid"), w));
        sb.AppendLine();
        sb.AppendLine(Center(string.IsNullOrWhiteSpace(opts?.FooterText) ? T("thanks") : opts.FooterText, w));
        sb.AppendLine();
        sb.AppendLine();
        return sb.ToString();
    }

    private static string Center(string s, int w)
    {
        s = s.Length > w ? s[..w] : s;
        var pad = (w - s.Length) / 2;
        return new string(' ', Math.Max(0, pad)) + s;
    }

    private static string Row(string left, string right, int w)
    {
        var space = w - left.Length - right.Length;
        return space > 0 ? left + new string(' ', space) + right : left + " " + right;
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
