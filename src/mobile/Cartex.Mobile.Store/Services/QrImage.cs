using QRCoder;

namespace Cartex.Mobile.Store.Services;

public static class QrImage
{
    public static ImageSource From(string value)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(value, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data).GetGraphic(12);
        return ImageSource.FromStream(() => new MemoryStream(png));
    }
}
