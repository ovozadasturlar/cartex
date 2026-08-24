using SkiaSharp;

namespace Cartex.UI.Services;

public static class EscPosImageHelper
{
    public static byte[] BinarizeToEscPosRaster(byte[] imageBytes, int targetWidth = 384, byte threshold = 128)
    {
        try
        {
            using var bitmap = SKBitmap.Decode(imageBytes);
            if (bitmap == null) return [];

            var width = (Math.Max(8, targetWidth) + 7) / 8 * 8;
            var contentHeight = Math.Max(1, (int)Math.Round((double)bitmap.Height / bitmap.Width * width));
            var height = (contentHeight + 7) / 8 * 8;

            using var resized = bitmap.Resize(new SKImageInfo(width, contentHeight), new SKSamplingOptions(SKFilterMode.Linear));
            if (resized == null) return [];

            int widthBytes = (width + 7) / 8;
            var rasterData = new byte[widthBytes * height];

            for (var y = 0; y < contentHeight; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var color = resized.GetPixel(x, y);
                    var luminance = (byte)(color.Red * 0.299 + color.Green * 0.587 + color.Blue * 0.114);
                    // If pixel is darker than threshold AND not fully transparent, it's a black dot.
                    if (luminance < threshold && color.Alpha > 128)
                    {
                        int byteIndex = y * widthBytes + x / 8;
                        rasterData[byteIndex] |= (byte)(1 << (7 - (x % 8)));
                    }
                }
            }

            var output = new List<byte>(rasterData.Length + 10);
            output.Add(0x1D); // GS
            output.Add(0x76); // v
            output.Add(0x30); // 0
            output.Add(0x00); // 0 (Normal mode)
            output.Add((byte)(widthBytes % 256)); // xL
            output.Add((byte)(widthBytes / 256)); // xH
            output.Add((byte)(height % 256));     // yL
            output.Add((byte)(height / 256));     // yH
            output.AddRange(rasterData);

            return output.ToArray();
        }
        catch
        {
            return [];
        }
    }
}
