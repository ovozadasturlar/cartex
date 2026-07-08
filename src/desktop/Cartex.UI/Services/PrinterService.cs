using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Cartex.Shared.Models.Sales;

namespace Cartex.UI.Services;

public record PrinterSettings(string? ReceiptPrinter, string? ZReportPrinter, string? BarcodePrinter, string? DocumentPrinter, string? ServerUrl, bool AutoPrintReceipt, double LabelWidthMm = 0, double LabelHeightMm = 0);

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
    void PrintReceipt(ReceiptDto receipt);
    void PrintRaw(string? printerName, string text);
    void PrintDocument(string filePath, string? printerName);
}

public sealed class PrinterService : IPrinterService
{
    private readonly string? _path;
    private PrinterSettings _settings = new(null, null, null, null, null, false);


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

    public void PrintReceipt(ReceiptDto receipt) => PrintRaw(_settings.ReceiptPrinter, FormatReceipt(receipt));

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

    private static string FormatReceipt(ReceiptDto r)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Center(r.BusinessName));
        if (!string.IsNullOrWhiteSpace(r.BranchName)) sb.AppendLine(Center(r.BranchName));
        if (!string.IsNullOrWhiteSpace(r.BranchAddress)) sb.AppendLine(Center(r.BranchAddress!));
        if (!string.IsNullOrWhiteSpace(r.BranchPhone)) sb.AppendLine(Center(r.BranchPhone!));
        sb.AppendLine(r.SaleDate.ToString("dd.MM.yyyy HH:mm"));
        if (!string.IsNullOrWhiteSpace(r.UserName)) sb.AppendLine($"Kassir: {r.UserName}");
        sb.AppendLine(new string('-', 32));
        foreach (var i in r.Items)
        {
            sb.AppendLine(i.ProductName);
            sb.AppendLine(Row($"  {i.Quantity:0.###} x {i.UnitPrice:N0}", $"{i.LineTotal:N0}"));
        }
        sb.AppendLine(new string('-', 32));
        if (r.DiscountAmount > 0) sb.AppendLine(Row("Chegirma", $"{r.DiscountAmount:N0}"));
        sb.AppendLine(Row("JAMI", $"{r.TotalAmount:N0}"));
        if (r.PaidCash > 0) sb.AppendLine(Row("Naqd", $"{r.PaidCash:N0}"));
        if (r.PaidCard > 0) sb.AppendLine(Row("Karta", $"{r.PaidCard:N0}"));
        if (r.PaidBonus > 0) sb.AppendLine(Row("Bonus", $"{r.PaidBonus:N0}"));
        if (r.ChangeAmount > 0) sb.AppendLine(Row("Qaytim", $"{r.ChangeAmount:N0}"));
        if (r.DebtAmount > 0) sb.AppendLine(Row("Qarz", $"{r.DebtAmount:N0}"));
        if (r.CashbackEarned > 0) sb.AppendLine(Row("Bonus to'plandi", $"{r.CashbackEarned:N0}"));
        sb.AppendLine(new string('-', 32));
        sb.AppendLine(Center(r.DebtAmount > 0 ? "QARZ" : "TO'LANDI"));
        sb.AppendLine();
        sb.AppendLine(Center("Rahmat!"));
        sb.AppendLine();
        sb.AppendLine();
        return sb.ToString();
    }

    private static string Center(string s)
    {
        s = s.Length > 32 ? s[..32] : s;
        var pad = (32 - s.Length) / 2;
        return new string(' ', Math.Max(0, pad)) + s;
    }

    private static string Row(string left, string right)
    {
        var space = 32 - left.Length - right.Length;
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
