namespace Cartex.UI.Services;

public enum PrinterKind
{
    Virtual,
    Label,
    ReceiptThermal,
    Document
}

public sealed record PrintTarget(string? Printer, bool IsDocument, string Paper);

/// The program adapts itself to whatever printers a shop actually has instead of
/// expecting somebody to configure every print type by hand.
public static class PrinterClassifier
{
    private static readonly string[] VirtualMarkers =
        ["print to pdf", "save to pdf", "xps", "onenote", "fax", "anydesk"];

    private static readonly string[] ReceiptMarkers =
        ["58mm", "80mm", "xp-58", "xp-80", "pos-58", "pos-80", "pos58", "pos80",
         "gp-58", "gp-80", "rp-58", "rp-80", "tm-t", "rongta", "bixolon", "sewoo", "star tsp", "receipt"];

    private static readonly string[] LabelMarkers =
        ["gprinter", "gp-", "xprinter", "tsc", "zebra", "godex", "argox", "label", "tspl"];

    public static PrinterKind Classify(string name, double? maxPaperWidthMm)
    {
        var value = name.ToLowerInvariant();
        if (VirtualMarkers.Any(value.Contains)) return PrinterKind.Virtual;
        if (ReceiptMarkers.Any(value.Contains)) return PrinterKind.ReceiptThermal;
        if (LabelMarkers.Any(value.Contains)) return PrinterKind.Label;
        return maxPaperWidthMm is > 0 and <= 120 ? PrinterKind.ReceiptThermal : PrinterKind.Document;
    }
}

public static class PrinterAutoSetup
{
    /// Fills the printer roles that are empty or whose printer is no longer installed.
    /// A choice the user made and that still exists is never touched.
    public static PrinterSettings Apply(
        PrinterSettings settings,
        IReadOnlyList<string> installed,
        Func<string, PrinterKind> kindOf,
        string? systemDefault)
    {
        bool Exists(string? printer) => !string.IsNullOrWhiteSpace(printer)
            && installed.Contains(printer, StringComparer.OrdinalIgnoreCase);

        string? Pick(string? current, PrinterKind kind, bool preferDefault = false)
        {
            if (Exists(current)) return current;
            var candidates = installed.Where(p => kindOf(p) == kind).ToList();
            if (preferDefault && systemDefault is not null
                && candidates.Contains(systemDefault, StringComparer.OrdinalIgnoreCase))
                return systemDefault;
            return candidates.FirstOrDefault();
        }

        return settings with
        {
            ReceiptPrinter = Pick(settings.ReceiptPrinter, PrinterKind.ReceiptThermal),
            BarcodePrinter = Pick(settings.BarcodePrinter, PrinterKind.Label),
            DocumentPrinter = Pick(settings.DocumentPrinter, PrinterKind.Document, preferDefault: true),
            ZReportPrinter = Exists(settings.ZReportPrinter) ? settings.ZReportPrinter : null
        };
    }
}

/// Chooses where and how a print type comes out on THIS device: the configured style
/// when its printer exists, otherwise whatever the device really has. The render style
/// always follows the physical printer, never the wish.
public static class PrintTargetResolver
{
    public static PrintTarget Receipt(PrinterSettings s, Func<string, PrinterKind> kindOf, Func<string?, bool> exists) =>
        For(s.ReceiptMode is "a4" or "a5" ? s.ReceiptMode : "thermal", null, s, kindOf, exists);

    public static PrintTarget Proforma(
        ProformaPrintOptions options,
        PrinterSettings s,
        Func<string, PrinterKind> kindOf,
        Func<string?, bool> exists) =>
        For(options.PaperFormat is "A4" or "A5" ? options.PaperFormat.ToLowerInvariant() : "thermal",
            null, s, kindOf, exists);

    public static PrintTarget ZReport(PrinterSettings s, Func<string, PrinterKind> kindOf, Func<string?, bool> exists) =>
        For(s.ZReportMode is "a4" or "a5" ? (s.ZReportDocumentPaperSize == "a5" ? "a5" : "a4") : "thermal",
            s.ZReportPrinter, s, kindOf, exists);

    private static PrintTarget For(
        string preference,
        string? dedicated,
        PrinterSettings s,
        Func<string, PrinterKind> kindOf,
        Func<string?, bool> exists)
    {
        var thermal = exists(s.ReceiptPrinter) ? s.ReceiptPrinter : null;
        var document = exists(s.DocumentPrinter) ? s.DocumentPrinter : null;
        var wantsDocument = preference is "a4" or "a5";
        var paper = wantsDocument ? preference : s.DocumentPaperSize == "a5" ? "a5" : "a4";
        var printer = exists(dedicated) ? dedicated
            : wantsDocument ? document ?? thermal : thermal ?? document;
        return printer is null
            ? new PrintTarget(null, wantsDocument, paper)
            : new PrintTarget(printer, kindOf(printer) is not (PrinterKind.ReceiptThermal or PrinterKind.Label), paper);
    }
}
