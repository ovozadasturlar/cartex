namespace Cartex.Shared.Models.Settings;

public readonly record struct PaperDimensions(double WidthMm, double HeightMm);

public readonly record struct PrintGrid(int Columns, int Rows);

public readonly record struct PrintPageSize(double Width, double Height);

public static class DocumentPrintLayout
{
    public static string ResolveOutputFormat(string outputType, string physicalPaperSize) =>
        outputType == "document"
            ? physicalPaperSize == "a5" ? "a5" : "a4"
            : "thermal";

    public static PaperDimensions GetPaperDimensions(string paperSize, string orientation)
    {
        var dimensions = paperSize == "a5"
            ? new PaperDimensions(148, 210)
            : new PaperDimensions(210, 297);

        return orientation == "landscape"
            ? new PaperDimensions(dimensions.HeightMm, dimensions.WidthMm)
            : dimensions;
    }

    public static PrintGrid GetGrid(string physicalOrientation, int pagesPerSheet)
    {
        if (pagesPerSheet == 4)
            return new PrintGrid(2, 2);

        if (pagesPerSheet == 2)
            return physicalOrientation == "landscape"
                ? new PrintGrid(2, 1)
                : new PrintGrid(1, 2);

        return new PrintGrid(1, 1);
    }

    public static string GetReceiptOrientation(string physicalOrientation, int pagesPerSheet) =>
        pagesPerSheet == 2
            ? physicalOrientation == "landscape" ? "portrait" : "landscape"
            : physicalOrientation == "landscape" ? "landscape" : "portrait";

    public static PrintPageSize FitWithoutUpscaling(
        PaperDimensions logicalPage,
        double unitsPerMmX,
        double unitsPerMmY,
        double slotWidth,
        double slotHeight)
    {
        var desiredWidth = logicalPage.WidthMm * unitsPerMmX;
        var desiredHeight = logicalPage.HeightMm * unitsPerMmY;
        var scale = Math.Min(
            1,
            Math.Min(slotWidth / desiredWidth, slotHeight / desiredHeight));

        return new PrintPageSize(desiredWidth * scale, desiredHeight * scale);
    }

    public static PrintPageSize GetPreviewPageSize(
        string physicalPaperSize,
        string receiptPaperSize,
        string physicalOrientation,
        int pagesPerSheet,
        double previewPaperWidth,
        double previewPaperHeight)
    {
        var physicalPage = GetPaperDimensions(physicalPaperSize, physicalOrientation);
        var receiptOrientation = GetReceiptOrientation(physicalOrientation, pagesPerSheet);
        var logicalPage = GetPaperDimensions(receiptPaperSize, receiptOrientation);
        var grid = GetGrid(physicalOrientation, pagesPerSheet);

        return FitWithoutUpscaling(
            logicalPage,
            previewPaperWidth / physicalPage.WidthMm,
            previewPaperHeight / physicalPage.HeightMm,
            previewPaperWidth / grid.Columns,
            previewPaperHeight / grid.Rows);
    }
}
