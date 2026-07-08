using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Cartex.Shared.Localization;
using Cartex.Shared.Models.Sales;

namespace Cartex.UI.Services;

public record PrinterSettings(string? ReceiptPrinter, string? ZReportPrinter, string? BarcodePrinter, string? DocumentPrinter, bool AutoPrintReceipt, double LabelWidthMm = 0, double LabelHeightMm = 0, string? ReceiptMode = null, int ReceiptPaperWidth = 0);

public record ReceiptPrintOptions(string? HeaderText, string? FooterText, int Width);

public static class LabelSize
{
    public static (double Width, double Height) Resolve(double width, double height) =>
        (width is < 20 or > 120 ? 58 : width, height is < 20 or > 120 ? 40 : height);
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
    void PrintRaw(string? printerName, string text);
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
            var psi = new ProcessStartInfo("powershell",
                "-NoProfile -Command \"Get-Printer | Select-Object -ExpandProperty Name\"")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc is null) return [];
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(5000);
            return output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
        catch { return []; }
    }

    public void PrintReceipt(ReceiptDto receipt)
    {
        var opts = _settings.ReceiptPaperWidth is 32 or 42 or 48
            ? new ReceiptPrintOptions(ReceiptOptions?.HeaderText, ReceiptOptions?.FooterText, _settings.ReceiptPaperWidth)
            : ReceiptOptions;
        PrintRaw(_settings.ReceiptPrinter, FormatReceipt(receipt, opts));
    }

    public void PrintRaw(string? printerName, string text)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(printerName)) return;
        RawPrinter.SendString(printerName, text);
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
        if (r.PaidCash > 0) sb.AppendLine(Row(T("cash"), $"{r.PaidCash:N0}", w));
        if (r.PaidCard > 0) sb.AppendLine(Row(T("card"), $"{r.PaidCard:N0}", w));
        if (r.PaidBonus > 0) sb.AppendLine(Row(T("bonus"), $"{r.PaidBonus:N0}", w));
        if (r.ChangeAmount > 0) sb.AppendLine(Row(T("change"), $"{r.ChangeAmount:N0}", w));
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

    public static void SendString(string printerName, string text)
    {
        if (!OperatingSystem.IsWindows()) return;
        var bytes = Encoding.UTF8.GetBytes(text);
        var unmanaged = Marshal.AllocCoTaskMem(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, unmanaged, bytes.Length);
            if (!OpenPrinter(printerName, out var hPrinter, IntPtr.Zero)) return;
            try
            {
                var di = new DOCINFO { DocName = "Cartex Receipt", DataType = "RAW" };
                if (!StartDocPrinter(hPrinter, 1, ref di)) return;
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
