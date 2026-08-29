using Cartex.UI.Services;
using Cartex.Shared.Models.Shifts;
using SkiaSharp;
using Xunit;

namespace Cartex.UnitTests;

public sealed class ZReportDocumentRendererTests
{
    private static readonly ZReportDto Report = new(
        1048, 100_000, 1_250_000, 25_000, 50_000, 30_000, 200_000, 75_000, 1_470_000, 1_470_000, 0)
    {
        CardSales = 480_000,
        BonusUsed = 35_000,
        NewDebtIssued = 90_000,
        SalesCount = 18
    };

    private static readonly ZReportDocumentMetadata Metadata = new(
        "Cartex Market",
        "Markaziy filial",
        "Akmal",
        new DateTime(2026, 7, 29, 15, 30, 0));

    [Fact]
    public void A5Portrait_Render_UsesExactPhysicalDimensionsAt150Dpi()
    {
        var pages = ZReportDocumentRenderer.Render(
            Report,
            Metadata,
            "a5",
            "portrait",
            false);

        Assert.Single(pages);
        using var bitmap = SKBitmap.Decode(pages[0]);
        Assert.NotNull(bitmap);
        Assert.Equal(874, bitmap.Width);
        Assert.Equal(1240, bitmap.Height);
    }

    [Fact]
    public void MonochromePrinter_Render_ContainsOnlyGrayscalePixels()
    {
        var pages = ZReportDocumentRenderer.Render(
            Report,
            Metadata,
            "a4",
            "portrait",
            false);

        using var bitmap = SKBitmap.Decode(pages[0]);
        Assert.NotNull(bitmap);
        var allPixelsAreGrayscale = true;
        SKColor firstNonGrayscale = default;
        var firstNonGrayscaleX = -1;
        var firstNonGrayscaleY = -1;
        for (var y = 0; y < bitmap.Height && allPixelsAreGrayscale; y++)
        for (var x = 0; x < bitmap.Width; x++)
        {
            var color = bitmap.GetPixel(x, y);
            if (color.Red != color.Green || color.Green != color.Blue)
            {
                allPixelsAreGrayscale = false;
                firstNonGrayscale = color;
                firstNonGrayscaleX = x;
                firstNonGrayscaleY = y;
                break;
            }
        }

        Assert.True(
            allPixelsAreGrayscale,
            $"First non-grayscale pixel at ({firstNonGrayscaleX}, {firstNonGrayscaleY}): {firstNonGrayscale}");
    }

    [Fact]
    public void ColorPrinter_Render_UsesBlueTitleAccent()
    {
        var pages = ZReportDocumentRenderer.Render(
            Report,
            Metadata,
            "a4",
            "portrait",
            true);

        using var bitmap = SKBitmap.Decode(pages[0]);
        Assert.NotNull(bitmap);
        var hasBluePixel = false;
        for (var y = 0; y < bitmap.Height && !hasBluePixel; y++)
        for (var x = 0; x < bitmap.Width; x++)
        {
            var color = bitmap.GetPixel(x, y);
            if (color.Blue > color.Red + 20)
            {
                hasBluePixel = true;
                break;
            }
        }

        Assert.True(hasBluePixel);
    }

    [Fact]
    public void CurrencyBreakdown_Render_AddsOneCompletePagePerCurrency()
    {
        var report = Report with
        {
            Currencies =
            [
                new("UZS", 100_000, 1_250_000, 25_000, 200_000, 75_000, 1_450_000, 1_450_000, 0),
                new("USD", 100, 400, 10, 50, 25, 515, 500, -15)
            ]
        };

        var pages = ZReportDocumentRenderer.Render(
            report,
            Metadata,
            "a4",
            "portrait",
            false);

        Assert.Equal(3, pages.Count);
        Assert.All(pages, page => Assert.NotEmpty(page));
    }
}
