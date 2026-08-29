using Cartex.Application.Common.Settings;
using Cartex.Application.Sales.Queries;
using Cartex.Infrastructure.Notifications;
using Xunit;
using Cartex.Shared.Models.Sales;

namespace Cartex.UnitTests;

public sealed class ReceiptPdfRendererTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Document_image_renders_for_all_supported_sizes_and_orientations(bool a4, bool landscape)
    {
        var renderer = new ReceiptPdfRenderer();
        var receipt = new ReceiptDto(
            "sample-token",
            "Cartex Market",
            "Markaziy filial",
            "Toshkent shahri",
            "+998 90 123 45 67",
            DateTime.UtcNow,
            125_000,
            5_000,
            130_000,
            0,
            0,
            0,
            5_000,
            2_500,
            "Admin",
            [
                new ReceiptItemDto("Sinov mahsuloti", 2, "dona", 65_000, 130_000)
            ],
            [
                new ReceiptPaymentDto("Cash", "UZS", 130_000)
            ],
            42,
            "Mijoz",
            "uz-latn");
        var settings = new ReceiptSettings
        {
            HeaderText = "STIR: 123456789",
            FooterText = "Xaridingiz uchun rahmat!",
            ShowQrCode = true,
            ShowElectronicLink = true,
            PublicReceiptBaseUrl = "https://receipt.example"
        };

        var images = renderer.RenderDocumentImages(receipt, settings, a4, landscape);

        var image = Assert.Single(images);
        Assert.NotNull(image);
        Assert.True(image.Length > 1000);
        Assert.Equal([0x89, 0x50, 0x4E, 0x47], image[..4]);
    }

    [Fact]
    public void Long_document_receipt_returns_every_page()
    {
        var receipt = new ReceiptDto(
            "long-token",
            "Cartex Market",
            "Markaziy filial",
            null,
            null,
            DateTime.UtcNow,
            12_500_000,
            0,
            12_500_000,
            0,
            0,
            0,
            0,
            0,
            "Admin",
            Enumerable.Range(1, 120)
                .Select(i => new ReceiptItemDto($"Mahsulot {i}", 1, "dona", 100_000, 100_000))
                .ToList(),
            [],
            43);

        var images = new ReceiptPdfRenderer().RenderDocumentImages(receipt, new ReceiptSettings(), a4: false);

        Assert.True(images.Count > 1);
        Assert.All(images, image => Assert.Equal([0x89, 0x50, 0x4E, 0x47], image[..4]));
    }
}
