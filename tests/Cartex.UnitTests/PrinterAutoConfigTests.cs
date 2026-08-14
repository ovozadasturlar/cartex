using Cartex.UI.Services;
using Xunit;

namespace Cartex.UnitTests;

public sealed class PrinterAutoConfigTests
{
    private static readonly string[] Installed =
        ["HP LaserJet MFP M227fdn UPD PCL 6", "Gprinter GP-3120TUD", "XP-80C", "Microsoft Print to PDF"];

    private static PrinterKind KindOf(string name) => name switch
    {
        "HP LaserJet MFP M227fdn UPD PCL 6" => PrinterKind.Document,
        "Gprinter GP-3120TUD" => PrinterKind.Label,
        "XP-80C" => PrinterKind.ReceiptThermal,
        _ => PrinterKind.Virtual
    };

    private static bool Exists(string? name) =>
        !string.IsNullOrWhiteSpace(name) && Installed.Contains(name, StringComparer.OrdinalIgnoreCase);

    [Theory]
    [InlineData("Microsoft Print to PDF", null, PrinterKind.Virtual)]
    [InlineData("Microsoft XPS Document Writer", null, PrinterKind.Virtual)]
    [InlineData("Gprinter GP-3120TUD", 118.0, PrinterKind.Label)]
    [InlineData("XP-80C", 80.0, PrinterKind.ReceiptThermal)]
    [InlineData("Rongta RP328", 80.0, PrinterKind.ReceiptThermal)]
    [InlineData("HP LaserJet MFP M227fdn UPD PCL 6", 216.0, PrinterKind.Document)]
    [InlineData("Noma'lum POS58 printer", null, PrinterKind.ReceiptThermal)]
    [InlineData("Noma'lum qurilma", 72.0, PrinterKind.ReceiptThermal)]
    [InlineData("Noma'lum qurilma", 210.0, PrinterKind.Document)]
    [InlineData("Noma'lum qurilma", null, PrinterKind.Document)]
    public void Classify_recognises_printer_kinds(string name, double? widthMm, PrinterKind expected) =>
        Assert.Equal(expected, PrinterClassifier.Classify(name, widthMm));

    [Fact]
    public void AutoSetup_fills_empty_roles_and_keeps_user_choices()
    {
        var settings = new PrinterSettings { DocumentPrinter = "Gprinter GP-3120TUD" };

        var updated = PrinterAutoSetup.Apply(settings, Installed, KindOf, "HP LaserJet MFP M227fdn UPD PCL 6");

        Assert.Equal("XP-80C", updated.ReceiptPrinter);
        Assert.Equal("Gprinter GP-3120TUD", updated.BarcodePrinter);
        Assert.Equal("Gprinter GP-3120TUD", updated.DocumentPrinter);
    }

    [Fact]
    public void AutoSetup_replaces_a_printer_that_was_uninstalled()
    {
        var settings = new PrinterSettings { ReceiptPrinter = "Eski XP-58", DocumentPrinter = "Eski Canon" };

        var updated = PrinterAutoSetup.Apply(settings, Installed, KindOf, "HP LaserJet MFP M227fdn UPD PCL 6");

        Assert.Equal("XP-80C", updated.ReceiptPrinter);
        Assert.Equal("HP LaserJet MFP M227fdn UPD PCL 6", updated.DocumentPrinter);
    }

    [Fact]
    public void AutoSetup_never_assigns_virtual_printers()
    {
        var updated = PrinterAutoSetup.Apply(new PrinterSettings(), ["Microsoft Print to PDF"], KindOf, "Microsoft Print to PDF");

        Assert.Null(updated.ReceiptPrinter);
        Assert.Null(updated.BarcodePrinter);
        Assert.Null(updated.DocumentPrinter);
    }

    [Fact]
    public void Receipt_uses_the_selected_thermal_printer_when_it_exists()
    {
        var settings = new PrinterSettings { ReceiptPrinter = "XP-80C", DocumentPrinter = "HP LaserJet MFP M227fdn UPD PCL 6" };

        var target = PrintTargetResolver.Receipt(settings, KindOf, Exists);

        Assert.Equal("XP-80C", target.Printer);
        Assert.False(target.IsDocument);
    }

    [Fact]
    public void Receipt_falls_back_to_the_document_printer_when_no_thermal_exists()
    {
        var settings = new PrinterSettings
        {
            DocumentPrinter = "HP LaserJet MFP M227fdn UPD PCL 6",
            DocumentPaperSize = "a5"
        };

        var target = PrintTargetResolver.Receipt(settings, KindOf, Exists);

        Assert.Equal("HP LaserJet MFP M227fdn UPD PCL 6", target.Printer);
        Assert.True(target.IsDocument);
        Assert.Equal("a5", target.Paper);
    }

    [Fact]
    public void Proforma_thermal_preference_lands_on_the_document_printer_when_thermal_is_missing()
    {
        var settings = new PrinterSettings { DocumentPrinter = "HP LaserJet MFP M227fdn UPD PCL 6" };
        var options = new ProformaPrintOptions(null, null, 32, "Thermal");

        var target = PrintTargetResolver.Proforma(options, settings, KindOf, Exists);

        Assert.Equal("HP LaserJet MFP M227fdn UPD PCL 6", target.Printer);
        Assert.True(target.IsDocument);
    }

    [Fact]
    public void Proforma_document_preference_lands_on_the_thermal_printer_when_no_document_printer_exists()
    {
        var settings = new PrinterSettings { ReceiptPrinter = "XP-80C" };
        var options = new ProformaPrintOptions(null, null, 32, "A5");

        var target = PrintTargetResolver.Proforma(options, settings, KindOf, Exists);

        Assert.Equal("XP-80C", target.Printer);
        Assert.False(target.IsDocument);
    }

    [Fact]
    public void ZReport_render_style_follows_the_dedicated_printer_kind()
    {
        var settings = new PrinterSettings { ZReportPrinter = "XP-80C", ZReportMode = "a4" };

        var target = PrintTargetResolver.ZReport(settings, KindOf, Exists);

        Assert.Equal("XP-80C", target.Printer);
        Assert.False(target.IsDocument);
    }

    [Fact]
    public void Nothing_is_resolved_when_the_device_has_no_usable_printer()
    {
        var target = PrintTargetResolver.Receipt(new PrinterSettings(), KindOf, Exists);

        Assert.Null(target.Printer);
    }
}
