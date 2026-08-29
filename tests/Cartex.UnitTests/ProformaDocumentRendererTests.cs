using Cartex.UI.Services;
using SkiaSharp;
using Xunit;

namespace Cartex.UnitTests;

public sealed class ProformaDocumentRendererTests
{
    private static readonly ProformaPrintOptions Options = new(
        "Xarid uchun rahmat", "Kassaga murojaat qiling", 32, "A4");

    private static PreviewDocument Document(int lines) => new(
        new DateTime(2026, 7, 29, 15, 30, 0),
        "Akmal",
        "Dilshod",
        [.. Enumerable.Range(1, lines).Select(i => new PreviewLine($"Mahsulot {i}", 2, "dona", 12_500, 25_000))],
        5_000,
        lines * 25_000m - 5_000,
        "Mijoz keyinroq oladi");

    [Fact]
    public void A4Portrait_Render_UsesExactPhysicalDimensionsAt150Dpi()
    {
        var pages = ProformaDocumentRenderer.Render(Document(5), "S-1024", null, Options, "a4", false);

        Assert.Single(pages);
        using var bitmap = SKBitmap.Decode(pages[0]);
        Assert.NotNull(bitmap);
        Assert.Equal(1240, bitmap.Width);
        Assert.Equal(1754, bitmap.Height);
    }

    [Fact]
    public void LongCart_Render_FlowsRowsAcrossPages()
    {
        var pages = ProformaDocumentRenderer.Render(Document(120), "S-1024", null, Options, "a5", false);

        Assert.True(pages.Count > 1);
        Assert.All(pages, page => Assert.NotEmpty(page));
    }

    [Fact]
    public void MonochromePrinter_Render_ContainsOnlyGrayscalePixels()
    {
        var pages = ProformaDocumentRenderer.Render(Document(3), null, null, Options, "a4", false);

        using var bitmap = SKBitmap.Decode(pages[0]);
        Assert.NotNull(bitmap);
        for (var y = 0; y < bitmap.Height; y++)
        for (var x = 0; x < bitmap.Width; x++)
        {
            var color = bitmap.GetPixel(x, y);
            Assert.True(color.Red == color.Green && color.Green == color.Blue,
                $"Non-grayscale pixel at ({x}, {y}): {color}");
        }
    }
}
