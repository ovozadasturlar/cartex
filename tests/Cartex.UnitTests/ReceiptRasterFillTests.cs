using Cartex.Shared.Models.Printing;
using Cartex.Shared.Models.Sales;
using Cartex.UI.Services;
using Xunit;

namespace Cartex.UnitTests;

public sealed class ReceiptRasterFillTests
{
    [Theory]
    [InlineData("normal")]
    [InlineData("large")]
    [InlineData("xlarge")]
    public void Raster_FillsThePaperWidth_AtEveryTextSize(string size)
    {
        const int dots = 576;
        var columns = ReceiptPaper.Sanitize(ReceiptPaper.ApplyTextSize(48, size));
        var document = ReceiptTextFormatter.Format(Receipt(), new ReceiptTextOptions(null, null, columns, Template: "lines"));

        var raster = ReceiptRasterRenderer.Render(document, dots, "medium", "standard").BeforeQr;
        var (widthBytes, height, right) = Inspect(raster);

        Assert.Equal(dots / 8, widthBytes);
        Assert.True(right >= dots * 0.9, $"{size}: columns={columns} widthBytes={widthBytes} height={height} ink reaches only {right} of {dots} dots");
    }

    private static (int WidthBytes, int Height, int Right) Inspect(byte[] raster)
    {
        var widthBytes = raster[4] | (raster[5] << 8);
        var height = raster[6] | (raster[7] << 8);
        var right = 0;
        for (var y = 0; y < height; y++)
            for (var b = 0; b < widthBytes; b++)
            {
                var value = raster[8 + y * widthBytes + b];
                if (value == 0) continue;
                for (var bit = 0; bit < 8; bit++)
                    if ((value & (1 << (7 - bit))) != 0)
                        right = Math.Max(right, b * 8 + bit + 1);
            }
        return (widthBytes, height, right);
    }

    private static ReceiptDto Receipt() => new(
        "t", "Cartex Biznes", "Asosiy filial", "Toshkent shahri, Amir Temur ko'chasi 12",
        "+998 90 123 45 67", new DateTime(2026, 8, 24, 20, 26, 0),
        81_000, 3_000, 81_000, 0, 0, 0, 0, 0, "Developer",
        [new ReceiptItemDto("Elektr kabeli", 2, "dona", 12_500, 25_000),
         new ReceiptItemDto("Mahkamlash to'plami", 1, "dona", 11_000, 11_000)],
        [new ReceiptPaymentDto("Cash", "UZS", 81_000, 1, 81_000)],
        1048, "Dilshod", "+998 90 555 12 34", null, "uz-latn", null);
}
