using Cartex.Shared.Models.Printing;
using Cartex.UI.Services;
using Xunit;

namespace Cartex.UnitTests;

/// ESC/POS spetsifikatsiyasidan yozilgan: chek ishi doim ESC @ (reset) bilan boshlanadi,
/// FS . bilan xitoycha ikki-baytli rejim o'chiriladi, ESC t bilan kod jadvali tanlanadi,
/// matn shu jadval kodlashida yuboriladi va ish oxirida qog'oz suriladi hamda kesiladi.
public sealed class EscPosTests
{
    private static EscPosProfile Profile(
        string charset = "cp437", int codeTable = -1, string cut = "partial", int feed = 4) =>
        new(EscPos.ResolveCharset(charset), codeTable, cut, feed);

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length && match; j++)
                match = haystack[i + j] == needle[j];
            if (match) return i;
        }
        return -1;
    }

    [Fact]
    public void Document_StartsWithReset_ChineseModeOff_AndCodeTable()
    {
        var bytes = EscPos.BuildDocument("TEST\n", null, null, Profile());

        Assert.Equal([0x1B, 0x40, 0x1C, 0x2E, 0x1B, 0x74, 0x00], bytes.Take(7));
    }

    [Fact]
    public void Cp866_SelectsTable17_AndEncodesCyrillic()
    {
        var bytes = EscPos.BuildDocument("Чек", null, null, Profile("cp866"));

        Assert.True(IndexOf(bytes, [0x1B, 0x74, 0x11]) >= 0);
        Assert.True(IndexOf(bytes, [0x97, 0xA5, 0xAA]) >= 0);
    }

    [Fact]
    public void ExplicitCodeTable_OverridesCharsetDefault()
    {
        var bytes = EscPos.BuildDocument("A", null, null, Profile("cp866", codeTable: 73));

        Assert.True(IndexOf(bytes, [0x1B, 0x74, 0x49]) >= 0);
    }

    [Fact]
    public void Utf8_SkipsCodeTable_KeepsMultibyteText()
    {
        var bytes = EscPos.BuildDocument("Чек", null, null, Profile("utf8"));

        Assert.Equal(-1, IndexOf(bytes, [0x1B, 0x74]));
        Assert.True(IndexOf(bytes, [0xD0, 0xA7, 0xD0, 0xB5, 0xD0, 0xBA]) >= 0);
    }

    [Fact]
    public void PartialCut_FeedsThenCuts()
    {
        var bytes = EscPos.BuildDocument("X\n", null, null, Profile(feed: 4));

        var feed = IndexOf(bytes, [0x1B, 0x64, 0x04]);
        var cut = IndexOf(bytes, [0x1D, 0x56, 0x42, 0x00]);
        Assert.True(feed >= 0);
        Assert.True(cut > feed);
        Assert.Equal(bytes.Length - 4, cut);
    }

    [Fact]
    public void FullCut_UsesFunctionA()
    {
        var bytes = EscPos.BuildDocument("X\n", null, null, Profile(cut: "full"));

        Assert.True(IndexOf(bytes, [0x1D, 0x56, 0x41, 0x00]) >= 0);
    }

    [Fact]
    public void NoCut_StillFeeds_ButNeverSendsCutCommand()
    {
        var bytes = EscPos.BuildDocument("X\n", null, null, Profile(cut: "none", feed: 3));

        Assert.True(IndexOf(bytes, [0x1B, 0x64, 0x03]) >= 0);
        Assert.Equal(-1, IndexOf(bytes, [0x1D, 0x56]));
    }

    [Fact]
    public void Typography_IsFlattenedToPrinterSafeAscii()
    {
        Assert.Equal("'''", EscPos.Sanitize("’‘ʻ"));
        Assert.Equal("---", EscPos.Sanitize("—–−"));
        Assert.Equal("No", EscPos.Sanitize("№"));
        Assert.Equal("\"\"\"\"", EscPos.Sanitize("«»“”"));
        Assert.Equal("~", EscPos.Sanitize("≈"));
        Assert.Equal("...", EscPos.Sanitize("…"));
        Assert.Equal(" ", EscPos.Sanitize(" "));
        Assert.Equal("x", EscPos.Sanitize("×"));
    }

    [Fact]
    public void UnmappableCharacter_BecomesQuestionMark_NotGarbageBytes()
    {
        var bytes = EscPos.BuildDocument("Чек", null, null, Profile("cp437"));

        Assert.True(IndexOf(bytes, [(byte)'?', (byte)'?', (byte)'?']) >= 0);
    }

    [Fact]
    public void QrAndLogo_KeepTheirPlaces_LogoAfterInit_QrBeforeCut()
    {
        var logo = new byte[] { 0x1D, 0x76, 0x30, 0x00 };
        var bytes = EscPos.BuildDocument("X\n", "https://r/1", logo, Profile());

        var logoAt = IndexOf(bytes, logo);
        var qrAt = IndexOf(bytes, [0x1D, 0x28, 0x6B]);
        var cutAt = IndexOf(bytes, [0x1D, 0x56]);
        Assert.Equal(7, logoAt);
        Assert.True(qrAt > logoAt);
        Assert.True(cutAt > qrAt);
    }

    [Fact]
    public void UnknownCharset_FallsBackToCp437()
    {
        var charset = EscPos.ResolveCharset("belgisiz");

        Assert.Equal("cp437", charset.Key);
        Assert.Equal(0, charset.CodeTable);
    }

    [Theory]
    [InlineData("cp866")]
    [InlineData("cp1251")]
    public void WP6_AutoMode_UsesGraphicForUzbekCyrillic(string charset)
    {
        var mode = EscPos.ResolveOutputMode("auto", "нақд тўлов ғолиб ҳақида", EscPos.ResolveCharset(charset));

        Assert.Equal("graphic", mode);
    }

    [Fact]
    public void WP6_AutoMode_UsesTextForUzbekLatinInCp1252()
    {
        var mode = EscPos.ResolveOutputMode("auto", "naqd to'lov amalga oshirildi", EscPos.ResolveCharset("cp1252"));

        Assert.Equal("text", mode);
    }

    [Fact]
    public void WP6_GraphicDocument_KeepsInitRasterQrAndCutOrder()
    {
        var raster = new byte[] { 0x1D, 0x76, 0x30, 0x00, 0x01, 0x00, 0x08, 0x00, 0x80, 0, 0, 0, 0, 0, 0, 0 };
        var bytes = EscPos.BuildDocument("нақд", "https://r/1", null, Profile("cp866"), raster);

        Assert.Equal([0x1B, 0x40], bytes.Take(2));
        var rasterAt = IndexOf(bytes, [0x1D, 0x76, 0x30, 0x00]);
        var qrAt = IndexOf(bytes, [0x1D, 0x28, 0x6B]);
        var cutAt = IndexOf(bytes, [0x1D, 0x56]);
        Assert.Equal(2, rasterAt);
        Assert.True(qrAt > rasterAt);
        Assert.True(cutAt > qrAt);
        Assert.Equal(-1, IndexOf(bytes, [0x1B, 0x74]));
    }

    [Fact]
    public void WP6_RasterRenderer_UsesExactAlignedPrinterGrid()
    {
        var raster = ReceiptRasterRenderer.Render("нақд\nJAMI 84 000\n", 384);

        Assert.Equal([0x1D, 0x76, 0x30, 0x00], raster.Take(4));
        Assert.Equal(48, raster[4] | raster[5] << 8);
        Assert.Equal(0, (raster[6] | raster[7] << 8) % 8);
    }
}

public sealed class ReceiptPaperTests
{
    [Theory]
    [InlineData(24, 24)]
    [InlineData(32, 32)]
    [InlineData(40, 40)]
    [InlineData(64, 64)]
    [InlineData(0, 32)]
    [InlineData(10, 32)]
    [InlineData(65, 32)]
    [InlineData(200, 32)]
    public void Sanitize_KeepsAnyRealWidth_FallsBackOtherwise(int width, int expected) =>
        Assert.Equal(expected, ReceiptPaper.Sanitize(width));

    [Fact]
    public void Sanitize_HonorsCustomFallback() =>
        Assert.Equal(0, ReceiptPaper.Sanitize(300, 0));

    [Theory]
    [InlineData(23, false)]
    [InlineData(24, true)]
    [InlineData(48, true)]
    [InlineData(64, true)]
    [InlineData(65, false)]
    public void IsValid_MatchesBounds(int width, bool expected) =>
        Assert.Equal(expected, ReceiptPaper.IsValid(width));

    /// Drayver qog'oz kengligini mm'da beradi; belgilar soni printer sinfidan chiqadi:
    /// 58 mm sinfi (45–67) → 32, 80 mm sinfi (68–99) → 48, 112 mm sinfi (100–120) → 64.
    /// Undan keng qog'oz chek printeri emas, undan tori noma'lum.
    [Theory]
    [InlineData(48.0, 32)]
    [InlineData(57.5, 32)]
    [InlineData(58.0, 32)]
    [InlineData(72.0, 48)]
    [InlineData(76.0, 48)]
    [InlineData(77.0, 48)]
    [InlineData(80.0, 48)]
    [InlineData(110.0, 64)]
    [InlineData(112.0, 64)]
    public void ColumnsForMm_MapsPaperClassToColumns(double mm, int expected) =>
        Assert.Equal(expected, ReceiptPaper.ColumnsForMm(mm));

    [Theory]
    [InlineData(30.0)]
    [InlineData(210.0)]
    public void ColumnsForMm_RejectsNonReceiptPaper(double mm) =>
        Assert.Null(ReceiptPaper.ColumnsForMm(mm));

    [Fact]
    public void ColumnsForMm_NullStaysNull() =>
        Assert.Null(ReceiptPaper.ColumnsForMm(null));
}
