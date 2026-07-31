using Cartex.Shared.Models.Settings;
using Xunit;

namespace Cartex.UnitTests;

public sealed class DocumentPrintLayoutTests
{
    [Theory]
    [InlineData("thermal", "a4", "thermal")]
    [InlineData("document", "a4", "a4")]
    [InlineData("document", "a5", "a5")]
    [InlineData("document", "unknown", "a4")]
    public void OutputFormat_IsDerivedFromOutputTypeAndPhysicalPaper(
        string outputType,
        string physicalPaper,
        string expected)
    {
        Assert.Equal(
            expected,
            DocumentPrintLayout.ResolveOutputFormat(outputType, physicalPaper));
    }

    [Fact]
    public void A5Receipt_OnA4Paper_KeepsA4PreviewAndUsesA5PhysicalSize()
    {
        var size = DocumentPrintLayout.GetPreviewPageSize(
            "a4",
            "a5",
            "portrait",
            1,
            310,
            438);

        Assert.Equal(218.48, size.Width, 2);
        Assert.Equal(309.70, size.Height, 2);
    }

    [Fact]
    public void A4Receipt_OnA4Paper_FillsThePreviewPaper()
    {
        var size = DocumentPrintLayout.GetPreviewPageSize(
            "a4",
            "a4",
            "portrait",
            1,
            310,
            438);

        Assert.Equal(310, size.Width, 2);
        Assert.Equal(438, size.Height, 2);
    }

    [Theory]
    [InlineData("landscape", 2, "portrait", 2, 1)]
    [InlineData("portrait", 2, "landscape", 1, 2)]
    [InlineData("landscape", 4, "landscape", 2, 2)]
    [InlineData("portrait", 4, "portrait", 2, 2)]
    public void ReceiptOrientationAndGrid_MatchPhysicalSheetSlots(
        string physicalOrientation,
        int pagesPerSheet,
        string receiptOrientation,
        int columns,
        int rows)
    {
        Assert.Equal(
            receiptOrientation,
            DocumentPrintLayout.GetReceiptOrientation(physicalOrientation, pagesPerSheet));
        Assert.Equal(
            new PrintGrid(columns, rows),
            DocumentPrintLayout.GetGrid(physicalOrientation, pagesPerSheet));
    }

    [Fact]
    public void FitWithoutUpscaling_DoesNotEnlargeA5PageInA4Slot()
    {
        var size = DocumentPrintLayout.FitWithoutUpscaling(
            new PaperDimensions(148, 210),
            10,
            10,
            2100,
            2970);

        Assert.Equal(1480, size.Width);
        Assert.Equal(2100, size.Height);
    }

    [Fact]
    public void A5ZReport_OnLandscapeA4WithTwoPages_FitsOnePhysicalHalf()
    {
        var size = DocumentPrintLayout.GetPreviewPageSize(
            "a4",
            "a5",
            "landscape",
            2,
            430,
            304);

        Assert.InRange(size.Width, 214, 215);
        Assert.Equal(304, size.Height, 2);
    }
}
