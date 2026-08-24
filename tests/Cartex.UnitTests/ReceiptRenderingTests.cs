using Cartex.Application.Common.Settings;
using Cartex.Infrastructure.Notifications;
using Cartex.Shared.Models.Printing;
using Cartex.Shared.Models.Sales;
using Cartex.UI.Services;
using Xunit;

namespace Cartex.UnitTests;

public sealed class ReceiptRenderingTests
{
    [Fact]
    public void WP6_BusinessPhone_IsUsedWhenBranchPhoneIsEmpty()
    {
        var text = PrinterService.FormatReceipt(Receipt(branchPhone: null, businessPhone: "+998 71 200 00 00"), Options());

        Assert.Contains("+998 71 200 00 00", text, StringComparison.Ordinal);
    }

    [Fact]
    public void WP6_PhoneLine_IsOmittedWhenBothPhonesAreEmpty()
    {
        var text = PrinterService.FormatReceipt(Receipt(branchPhone: null, businessPhone: null), Options());

        Assert.DoesNotContain("+998 90 100 00 00", text, StringComparison.Ordinal);
        Assert.DoesNotContain("+998 71 200 00 00", text, StringComparison.Ordinal);
    }

    [Fact]
    public void WP6_ShowPhoneFalse_HidesBothPhoneSources()
    {
        var text = PrinterService.FormatReceipt(
            Receipt(branchPhone: "+998 90 100 00 00", businessPhone: "+998 71 200 00 00"),
            Options(showPhone: false));

        Assert.DoesNotContain("+998 90 100 00 00", text, StringComparison.Ordinal);
        Assert.DoesNotContain("+998 71 200 00 00", text, StringComparison.Ordinal);
    }

    [Fact]
    public void WP6_BackendAndDesktop_UseTheSameReceiptText()
    {
        var receipt = Receipt(branchPhone: null, businessPhone: "+998 71 200 00 00");
        var desktop = PrinterService.FormatReceipt(receipt, Options(width: 48, template: "table"));
        var backend = ReceiptTextRenderer.Render(receipt, new ReceiptSettings
        {
            PaperWidth = 48,
            ShowPhone = true
        }, "table");

        Assert.Equal(desktop, backend);
    }

    [Fact]
    public void WP6_GraphicPreview_EqualsPrintableText()
    {
        var text = PrinterService.FormatReceipt(Receipt(language: "uz-cyrl"), Options());

        Assert.Equal(text, EscPos.PreviewText(text, "graphic", EscPos.ResolveCharset("cp866")));
    }

    [Fact]
    public void WP22_TwoLineTemplate_KeepsFullProductNameOnItsOwnLine()
    {
        var text = PrinterService.FormatReceipt(Receipt(), Options(width: 32, template: "lines"));

        Assert.Contains("1 Uzun mahsulot nomi sinov uchun", text, StringComparison.Ordinal);
    }

    [Fact]
    public void WP22_TwoLineTemplate_PutsMeasurePriceAndTotalOnTheNextLine()
    {
        var lines = PrinterService.FormatReceipt(Receipt(), Options(width: 32, template: "lines"))
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r'))
            .ToList();

        var nameIndex = lines.FindIndex(line => line.StartsWith("1 Uzun", StringComparison.Ordinal));
        var detail = lines[nameIndex + 1];

        Assert.StartsWith("  ", detail, StringComparison.Ordinal);
        Assert.Contains("2 dona", detail, StringComparison.Ordinal);
        Assert.Contains(42_000.ToString("N0"), detail, StringComparison.Ordinal);
        Assert.EndsWith(84_000.ToString("N0"), detail, StringComparison.Ordinal);
        Assert.True(detail.Length <= 32);
    }

    [Fact]
    public void WP22_LargerTextSize_ReducesColumnsSoGlyphsGrow()
    {
        Assert.Equal(48, ReceiptPaper.ApplyTextSize(48, "normal"));
        Assert.Equal(40, ReceiptPaper.ApplyTextSize(48, "large"));
        Assert.Equal(35, ReceiptPaper.ApplyTextSize(48, "xlarge"));
    }

    /// Yozuv o'lchami rasm kengligiga ta'sir qilsa chek torayib qog'ozning chap
    /// tomoniga surilib chiqadi — bu aynan sodir bo'lgan nosozlik.
    [Fact]
    public void WP22_RasterWidth_IgnoresTextSizeAndFillsThePaper()
    {
        const int nominal = 48;

        Assert.Equal(576, ReceiptPaper.RasterDots(0, null, nominal));
        Assert.Equal(
            ReceiptPaper.RasterDots(0, null, nominal),
            ReceiptPaper.RasterDots(0, null, nominal));
        Assert.NotEqual(
            ReceiptPaper.RasterDots(0, null, nominal),
            ReceiptPaper.RasterDots(0, null, ReceiptPaper.ApplyTextSize(nominal, "xlarge")));
    }

    [Fact]
    public void WP22_RasterWidth_PrefersExplicitThenDriverDots()
    {
        Assert.Equal(576, ReceiptPaper.RasterDots(576, 384, 32));
        Assert.Equal(384, ReceiptPaper.RasterDots(0, 384, 48));
        Assert.Equal(576, ReceiptPaper.RasterDots(0, 12, 48));
    }

    private static ReceiptPrintOptions Options(int width = 32, bool showPhone = true, string template = "auto") =>
        new(null, null, width, ShowPhone: showPhone, Template: template);

    private static ReceiptDto Receipt(
        string? branchPhone = "+998 90 100 00 00",
        string? businessPhone = null,
        string language = "uz-latn") =>
        new(
            "preview-token",
            "Cartex Market",
            "Chilonzor",
            "Toshkent",
            branchPhone,
            new DateTime(2026, 8, 24, 14, 30, 0),
            84_000,
            0,
            84_000,
            0,
            0,
            0,
            0,
            0,
            "Akmal",
            [new ReceiptItemDto("Uzun mahsulot nomi sinov uchun", 2, "dona", 42_000, 84_000)],
            [new ReceiptPaymentDto("Cash", "UZS", 84_000, 1, 84_000)],
            1048,
            "Dilshod",
            "+998 90 555 12 34",
            null,
            language,
            businessPhone);
}
