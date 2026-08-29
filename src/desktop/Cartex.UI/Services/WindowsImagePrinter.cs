using System.ComponentModel;
using System.Runtime.InteropServices;
using Cartex.Shared.Models.Settings;
using SkiaSharp;

namespace Cartex.UI.Services;

internal static class WindowsImagePrinter
{
    private const int DmOutBuffer = 2;
    private const int DmInBuffer  = 8;
    private const int DmOrientation    = 0x00000001;
    private const int DmPaperSize      = 0x00000002;
    private const int DmPaperLength    = 0x00000004;
    private const int DmPaperWidth     = 0x00000008;
    private const int DmDefaultSource  = 0x00000200;
    private const int DmColor          = 0x00000800;
    private const short DmOrientPortrait    = 1;
    private const short DmOrientLandscape   = 2;
    private const short DmColorMonochrome   = 1;
    private const short DmColorColor        = 2;
    private const short DmBinFormSource     = 15;
    private const short DcColorDevice = 32;
    private const int HorzRes    = 8;
    private const int VertRes    = 10;
    private const int LogPixelsX = 88;
    private const int LogPixelsY = 90;
    private const int DibRgbColors = 0;
    private const int SrcCopy      = 0x00CC0020;
    private const int Halftone     = 4;

    private static readonly IReadOnlyDictionary<string, short> PaperCodeMap =
        new Dictionary<string, short>(StringComparer.OrdinalIgnoreCase)
        {
            ["a3"]     = 8,
            ["a4"]     = 9,
            ["a5"]     = 11,
            ["letter"] = 1,
            ["legal"]  = 5,
        };

    private const short DcPaperSize = 3;

    /// Widest paper the driver can feed, in millimetres. A narrow maximum identifies a
    /// receipt or label printer regardless of what the printer happens to be called.
    public static double? MaxPaperWidthMm(string? printerName)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(printerName))
            return null;
        try
        {
            using var printers = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\Print\Printers");
            using var printer = printers?.OpenSubKey(printerName);
            var port = printer?.GetValue("Port") as string;
            var count = DeviceCapabilities(printerName, port, DcPaperSize, IntPtr.Zero, IntPtr.Zero);
            if (count <= 0) return null;
            // Twice the reported size: a buggy driver writing more entries on the second
            // call must corrupt slack space, not the heap.
            var capacity = count * 2;
            var buffer = Marshal.AllocHGlobal(capacity * 8);
            try
            {
                var written = DeviceCapabilities(printerName, port, DcPaperSize, buffer, IntPtr.Zero);
                if (written <= 0) return null;
                var max = 0;
                for (var i = 0; i < Math.Min(written, capacity); i++)
                {
                    var width = Marshal.ReadInt32(buffer, i * 8);
                    if (width > max) max = width;
                }
                return max > 0 ? max / 10.0 : null;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch
        {
            return null;
        }
    }

    public static int? PrintableWidthDots(string? printerName)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(printerName))
            return null;
        var dc = IntPtr.Zero;
        try
        {
            dc = CreateDC("WINSPOOL", printerName, null, IntPtr.Zero);
            if (dc == IntPtr.Zero) return null;
            var width = GetDeviceCaps(dc, HorzRes);
            return width > 0 ? width : null;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (dc != IntPtr.Zero) DeleteDC(dc);
        }
    }

    public static string? DefaultPrinter()
    {
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            var size = 256;
            var buffer = new System.Text.StringBuilder(size);
            return GetDefaultPrinterNative(buffer, ref size) ? buffer.ToString() : null;
        }
        catch
        {
            return null;
        }
    }

    public static bool SupportsColor(string? printerName)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(printerName))
            return false;

        try
        {
            using var printers = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\Print\Printers");
            using var printer = printers?.OpenSubKey(printerName);
            var port = printer?.GetValue("Port") as string;
            var capability = DeviceCapabilities(
                printerName,
                port,
                DcColorDevice,
                IntPtr.Zero,
                IntPtr.Zero);
            if (capability >= 0)
                return capability == 1;

            return ReadDefaultColorMode(printerName);
        }
        catch
        {
            return false;
        }
    }

    public static void Print(
        string printerName,
        IReadOnlyList<byte[]> imagePages,
        string paperSize,
        string receiptPaperSize,
        string orientation,
        int pagesPerSheet,
        int copies,
        string? outputFilePath = null)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var bitmaps = new List<SKBitmap>(imagePages.Count);
        try
        {
            var supportsColor = SupportsColor(printerName);
            foreach (var imageBytes in imagePages)
            {
                using var source = SKBitmap.Decode(imageBytes)
                    ?? throw new InvalidOperationException("Chek tasvirini o'qib bo'lmadi.");
                bitmaps.Add(ToPrintableBitmap(source, supportsColor));
            }
            PrintCore(
                printerName,
                bitmaps,
                paperSize,
                receiptPaperSize,
                orientation,
                pagesPerSheet,
                copies,
                supportsColor,
                outputFilePath);
        }
        finally
        {
            foreach (var bitmap in bitmaps)
                bitmap.Dispose();
        }
    }

    private static void PrintCore(
        string printerName,
        IReadOnlyList<SKBitmap> bitmaps,
        string paperSize,
        string receiptPaperSize,
        string orientation,
        int pagesPerSheet,
        int copies,
        bool supportsColor,
        string? outputFilePath = null)
    {
        if (!OpenPrinter(printerName, out var printer, IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Printer ochilmadi: {printerName}");

        IntPtr devMode = IntPtr.Zero;
        IntPtr dc = IntPtr.Zero;
        try
        {
            var paperCode  = PaperCodeMap.TryGetValue(paperSize, out var code) ? code : (short)9;
            var dimensions = DocumentPrintLayout.GetPaperDimensions(paperSize, orientation);
            devMode = CreateDevMode(
                printer,
                printerName,
                paperCode,
                orientation == "landscape" ? DmOrientLandscape : DmOrientPortrait,
                dimensions,
                supportsColor);
            dc = CreateDC("WINSPOOL", printerName, null, devMode);
            if (dc == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Printer konteksti yaratilmadi: {printerName}");

            var info = new DocInfo
            {
                Size = Marshal.SizeOf<DocInfo>(),
                DocName = "Cartex Receipt",
                Output = outputFilePath
            };
            if (StartDoc(dc, ref info) <= 0)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Chop etish vazifasi boshlanmadi.");

            try
            {
                var pagesOnSheet = pagesPerSheet is 2 or 4 ? pagesPerSheet : 1;
                for (var copy = 0; copy < Math.Clamp(copies, 1, 5); copy++)
                    for (var firstPage = 0; firstPage < bitmaps.Count; firstPage += pagesOnSheet)
                        PrintSheet(dc, bitmaps, firstPage, pagesOnSheet, receiptPaperSize, orientation);
            }
            finally
            {
                EndDoc(dc);
            }
        }
        finally
        {
            if (dc != IntPtr.Zero) DeleteDC(dc);
            if (devMode != IntPtr.Zero) Marshal.FreeHGlobal(devMode);
            ClosePrinter(printer);
        }
    }

    private static SKBitmap ToPrintableBitmap(SKBitmap source, bool supportsColor)
    {
        var target = new SKBitmap(new SKImageInfo(
            source.Width,
            source.Height,
            SKColorType.Bgra8888,
            SKAlphaType.Opaque));
        using var canvas = new SKCanvas(target);
        canvas.Clear(SKColors.White);
        if (supportsColor)
        {
            canvas.DrawBitmap(source, 0, 0);
        }
        else
        {
            using var paint = new SKPaint();
            paint.ColorFilter = SKColorFilter.CreateColorMatrix(
                [
                    0.2126f, 0.7152f, 0.0722f, 0, 0,
                    0.2126f, 0.7152f, 0.0722f, 0, 0,
                    0.2126f, 0.7152f, 0.0722f, 0, 0,
                    0,       0,       0,       1, 0
                ]);
            canvas.DrawBitmap(source, 0, 0, paint);
        }
        return target;
    }

    private static bool ReadDefaultColorMode(string printerName)
    {
        if (!OpenPrinter(printerName, out var printer, IntPtr.Zero))
            return false;

        IntPtr pointer = IntPtr.Zero;
        try
        {
            var size = DocumentProperties(IntPtr.Zero, printer, printerName, IntPtr.Zero, IntPtr.Zero, 0);
            if (size <= 0)
                return false;

            pointer = Marshal.AllocHGlobal(size);
            if (DocumentProperties(IntPtr.Zero, printer, printerName, pointer, IntPtr.Zero, DmOutBuffer) < 0)
                return false;

            var mode = Marshal.PtrToStructure<DevMode>(pointer);
            return (mode.Fields & DmColor) != 0 && mode.Color == DmColorColor;
        }
        finally
        {
            if (pointer != IntPtr.Zero)
                Marshal.FreeHGlobal(pointer);
            ClosePrinter(printer);
        }
    }

    private static IntPtr CreateDevMode(
        IntPtr printer,
        string printerName,
        short paperCode,
        short orientation,
        PaperDimensions dimensions,
        bool supportsColor)
    {
        var size = DocumentProperties(IntPtr.Zero, printer, printerName, IntPtr.Zero, IntPtr.Zero, 0);
        if (size <= 0)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Printer sozlamalari o'qilmadi.");

        var pointer = Marshal.AllocHGlobal(size);
        if (DocumentProperties(IntPtr.Zero, printer, printerName, pointer, IntPtr.Zero, DmOutBuffer) < 0)
        {
            Marshal.FreeHGlobal(pointer);
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Printer sozlamalari tayyorlanmadi.");
        }

        var mode = Marshal.PtrToStructure<DevMode>(pointer);
        mode.Fields        |= DmOrientation | DmPaperSize | DmPaperLength | DmPaperWidth | DmDefaultSource | DmColor;
        mode.Orientation    = orientation;
        mode.PaperSize      = paperCode;
        mode.PaperLength    = (short)Math.Round(dimensions.HeightMm * 10);
        mode.PaperWidth     = (short)Math.Round(dimensions.WidthMm  * 10);
        mode.DefaultSource  = DmBinFormSource;
        mode.FormName       = "";
        mode.Color          = supportsColor ? DmColorColor : DmColorMonochrome;
        Marshal.StructureToPtr(mode, pointer, false);

        if (DocumentProperties(IntPtr.Zero, printer, printerName, pointer, pointer, DmInBuffer | DmOutBuffer) < 0)
        {
            Marshal.FreeHGlobal(pointer);
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Tanlangan qog'oz sozlamasi printer tomonidan qabul qilinmadi.");
        }

        return pointer;
    }

    private static void PrintSheet(
        IntPtr dc,
        IReadOnlyList<SKBitmap> bitmaps,
        int firstPage,
        int pagesPerSheet,
        string receiptPaperSize,
        string physicalOrientation)
    {
        if (StartPage(dc) <= 0)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Printer sahifani boshlamadi.");

        try
        {
            var width = GetDeviceCaps(dc, HorzRes);
            var height = GetDeviceCaps(dc, VertRes);
            var dpiX = GetDeviceCaps(dc, LogPixelsX);
            var dpiY = GetDeviceCaps(dc, LogPixelsY);
            var grid = DocumentPrintLayout.GetGrid(physicalOrientation, pagesPerSheet);
            var columns = grid.Columns;
            var rows = grid.Rows;

            var slotWidth = width / columns;
            var slotHeight = height / rows;
            var pageCount = Math.Min(pagesPerSheet, bitmaps.Count - firstPage);
            SetStretchBltMode(dc, Halftone);

            for (var slot = 0; slot < pageCount; slot++)
            {
                var bitmap = bitmaps[firstPage + slot];
                var column = slot % columns;
                var row = slot / columns;
                var logicalOrientation = bitmap.Width >= bitmap.Height ? "landscape" : "portrait";
                var logicalPage = DocumentPrintLayout.GetPaperDimensions(
                    receiptPaperSize,
                    logicalOrientation);
                var printSize = DocumentPrintLayout.FitWithoutUpscaling(
                    logicalPage,
                    dpiX / 25.4,
                    dpiY / 25.4,
                    slotWidth,
                    slotHeight);
                var targetWidth = Math.Max(1, (int)Math.Floor(printSize.Width));
                var targetHeight = Math.Max(1, (int)Math.Floor(printSize.Height));
                var x = column * slotWidth + (slotWidth  - targetWidth)  / 2;
                var y = row    * slotHeight + (slotHeight - targetHeight) / 2;
                var bitmapInfo = new BitmapInfo
                {
                    Header = new BitmapInfoHeader
                    {
                        Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                        Width = bitmap.Width,
                        Height = -bitmap.Height,
                        Planes = 1,
                        BitCount = 32,
                        Compression = 0,
                        SizeImage = (uint)(bitmap.RowBytes * bitmap.Height)
                    }
                };

                var result = StretchDIBits(
                    dc,
                    x,
                    y,
                    targetWidth,
                    targetHeight,
                    0,
                    0,
                    bitmap.Width,
                    bitmap.Height,
                    bitmap.GetPixels(),
                    ref bitmapInfo,
                    DibRgbColors,
                    SrcCopy);
                if (result == 0)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Chek tasviri printerga uzatilmadi.");
            }
        }
        finally
        {
            EndPage(dc);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DevMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        public short SpecVersion;
        public short DriverVersion;
        public short Size;
        public short DriverExtra;
        public int Fields;
        public short Orientation;
        public short PaperSize;
        public short PaperLength;
        public short PaperWidth;
        public short Scale;
        public short Copies;
        public short DefaultSource;
        public short PrintQuality;
        public short Color;
        public short Duplex;
        public short YResolution;
        public short TTOption;
        public short Collate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string FormName;
        public short LogPixels;
        public int BitsPerPel;
        public int PelsWidth;
        public int PelsHeight;
        public int DisplayFlags;
        public int DisplayFrequency;
        public int ICMMethod;
        public int ICMIntent;
        public int MediaType;
        public int DitherType;
        public int Reserved1;
        public int Reserved2;
        public int PanningWidth;
        public int PanningHeight;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DocInfo
    {
        public int Size;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string DocName;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? Output;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? DataType;
        public uint Type;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ClrUsed;
        public uint ClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RgbQuad
    {
        public byte Blue;
        public byte Green;
        public byte Red;
        public byte Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public BitmapInfoHeader Header;
        public RgbQuad Colors;
    }

    [DllImport("winspool.drv", EntryPoint = "OpenPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool OpenPrinter(string printerName, out IntPtr printer, IntPtr defaults);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr printer);

    [DllImport("winspool.drv", EntryPoint = "DocumentPropertiesW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int DocumentProperties(IntPtr window, IntPtr printer, string deviceName, IntPtr output, IntPtr input, int mode);

    [DllImport("winspool.drv", EntryPoint = "DeviceCapabilitiesW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int DeviceCapabilities(
        string device,
        string? port,
        short capability,
        IntPtr output,
        IntPtr devMode);

    [DllImport("winspool.drv", EntryPoint = "GetDefaultPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool GetDefaultPrinterNative(System.Text.StringBuilder buffer, ref int size);

    [DllImport("gdi32.dll", EntryPoint = "CreateDCW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateDC(string driver, string device, string? output, IntPtr devMode);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr dc);

    [DllImport("gdi32.dll", EntryPoint = "StartDocW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int StartDoc(IntPtr dc, ref DocInfo info);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern int EndDoc(IntPtr dc);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern int StartPage(IntPtr dc);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern int EndPage(IntPtr dc);

    [DllImport("gdi32.dll")]
    private static extern int GetDeviceCaps(IntPtr dc, int index);

    [DllImport("gdi32.dll")]
    private static extern int SetStretchBltMode(IntPtr dc, int mode);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern int StretchDIBits(
        IntPtr dc,
        int xDestination,
        int yDestination,
        int destinationWidth,
        int destinationHeight,
        int xSource,
        int ySource,
        int sourceWidth,
        int sourceHeight,
        IntPtr bits,
        ref BitmapInfo bitmapInfo,
        int usage,
        int rasterOperation);
}
