using System.Text.Json;
using Cartex.UI.Services;
using Xunit;

namespace Cartex.UnitTests;

public sealed class PrinterSettingsTests
{
    [Fact]
    public void LegacyPrintingJson_LoadsWithSafeZReportDefaults()
    {
        const string json =
            """
            {
              "ReceiptPrinter": "Thermal",
              "DocumentPrinter": "Microsoft Print to PDF",
              "ReceiptMode": "a5",
              "DocumentPaperSize": "a4",
              "DocumentOrientation": "landscape",
              "DocumentPagesPerSheet": 2
            }
            """;

        var settings = JsonSerializer.Deserialize<PrinterSettings>(json);

        Assert.NotNull(settings);
        Assert.Null(settings.ZReportMode);
        Assert.Equal(0, settings.ZReportPaperWidth);
        Assert.Equal(1, settings.ZReportDocumentPagesPerSheet);
    }

    [Fact]
    public void ZReportPrintingSettings_RoundTripWithoutLosingLayout()
    {
        var settings = new PrinterSettings
        {
            ZReportPrinter = "Microsoft Print to PDF",
            AutoPrintZReport = true,
            ZReportMode = "a5",
            ZReportPaperWidth = 42,
            ZReportDocumentPaperSize = "a4",
            ZReportDocumentOrientation = "landscape",
            ZReportDocumentPagesPerSheet = 2
        };

        var restored = JsonSerializer.Deserialize<PrinterSettings>(
            JsonSerializer.Serialize(settings));

        Assert.NotNull(restored);
        Assert.Equal(settings, restored);
    }
}
