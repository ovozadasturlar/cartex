using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ZXing;
using ZXing.Common;

namespace Cartex.UI.Services;

public interface IBarcodeLabelService
{
    byte[] RenderPng(string code);
    void PrintLabels(string code, string name, int quantity, string? printerName);
}

public sealed class BarcodeLabelService(IPrinterService printer) : IBarcodeLabelService
{
    static BarcodeLabelService() => QuestPDF.Settings.License = LicenseType.Community;

    public byte[] RenderPng(string code) =>
        string.IsNullOrWhiteSpace(code) ? [] : RenderCode128(code);

    public void PrintLabels(string code, string name, int quantity, string? printerName)
    {
        if (string.IsNullOrWhiteSpace(code) || quantity < 1) return;

        var (width, height) = LabelSize.Resolve(printer.GetSettings().LabelWidthMm, printer.GetSettings().LabelHeightMm);
        Task.Run(() =>
        {
            try
            {
                var image = RenderCode128(code);
                var document = Document.Create(container =>
                {
                    for (var i = 0; i < quantity; i++)
                    {
                        container.Page(page =>
                        {
                            page.Size((float)width, (float)height, Unit.Millimetre);
                            page.Margin(height < 40 ? 2 : 3, Unit.Millimetre);
                            page.Content().Column(col =>
                            {
                                col.Spacing(1);
                                col.Item().AlignCenter().Text(name).FontSize(width < 50 ? 7 : 8).SemiBold();
                                col.Item().Image(image).FitWidth();
                                col.Item().AlignCenter().Text(code).FontSize(9).FontFamily("Consolas").LetterSpacing(0.05f);
                            });
                        });
                    }
                });

                var path = Path.Combine(Path.GetTempPath(), $"cartex-label-{Guid.NewGuid():N}.pdf");
                document.GeneratePdf(path);
                printer.PrintDocument(path, printerName ?? printer.BarcodePrinter);
            }
            catch { }
        });
    }

    private static byte[] RenderCode128(string code)
    {
        var writer = new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.CODE_128,
            Options = new EncodingOptions { Width = 360, Height = 120, Margin = 4, PureBarcode = true }
        };
        var pixelData = writer.Write(code);

        var bitmap = new WriteableBitmap(new PixelSize(pixelData.Width, pixelData.Height),
            new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using (var buffer = bitmap.Lock())
            Marshal.Copy(pixelData.Pixels, 0, buffer.Address, pixelData.Pixels.Length);

        using var stream = new MemoryStream();
        bitmap.Save(stream);
        return stream.ToArray();
    }
}
