using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Cartex.UI.Services;

public interface IBarcodeLabelService
{
    byte[] RenderLabelPreview(string code, string name, string? priceText, string? sku = null);
    LabelPreviewResult RenderLabelPreview(
        string code,
        string name,
        string? priceText,
        LabelOptions options,
        string? sku = null);
    void PrintLabels(string code, string name, int quantity, string? printerName, string? priceText = null, string? sku = null, LabelOptions? options = null);
}

public record LabelPreviewResult(byte[] Image, bool MayClip);

public sealed class BarcodeLabelService(IPrinterService printer) : IBarcodeLabelService
{
    static BarcodeLabelService() => QuestPDF.Settings.License = LicenseType.Community;

    public byte[] RenderLabelPreview(string code, string name, string? priceText, string? sku = null)
    {
        if (string.IsNullOrWhiteSpace(code)) return [];
        return TsplLabel.RenderPng(code, name, priceText, LabelSize.Resolve(printer.GetSettings()), sku);
    }

    public LabelPreviewResult RenderLabelPreview(
        string code,
        string name,
        string? priceText,
        LabelOptions options,
        string? sku = null)
    {
        if (string.IsNullOrWhiteSpace(code))
            return new LabelPreviewResult([], false);

        var result = TsplLabel.RenderPreview(code, name, priceText, options, sku);
        return new LabelPreviewResult(result.Image, result.MayClip);
    }

    public void PrintLabels(string code, string name, int quantity, string? printerName, string? priceText = null, string? sku = null, LabelOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(code) || quantity < 1) return;

        var settings = printer.GetSettings();
        var effectiveOptions = options ?? LabelSize.Resolve(settings);
        var (width, height) = (effectiveOptions.WidthMm, effectiveOptions.HeightMm);
        var target = printerName ?? printer.BarcodePrinter;

        if (!string.Equals(settings.LabelMode, "pdf", StringComparison.OrdinalIgnoreCase))
        {
            printer.PrintRawBytes(target, TsplLabel.Build(code, name, quantity, effectiveOptions, priceText, sku));
            return;
        }

        var image = TsplLabel.RenderPrintPng(code, name, priceText, effectiveOptions, sku);
        var document = Document.Create(container =>
        {
            for (var i = 0; i < quantity; i++)
            {
                container.Page(page =>
                {
                    page.Size((float)width, (float)height, Unit.Millimetre);
                    page.Margin(0);
                    page.Content().Image(image).FitArea();
                });
            }
        });

        var path = Path.Combine(Path.GetTempPath(), $"cartex-label-{Guid.NewGuid():N}.pdf");
        document.GeneratePdf(path);
        printer.PrintDocument(path, target);
    }
}
