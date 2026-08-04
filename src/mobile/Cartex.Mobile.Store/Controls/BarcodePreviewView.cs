namespace Cartex.Mobile.Store.Controls;

public sealed class BarcodePreviewView : GraphicsView, IDrawable
{
    private static readonly string[] Patterns =
    [
        "212222","222122","222221","121223","121322","131222","122213","122312","132212","221213",
        "221312","231212","112232","122132","122231","113222","123122","123221","223211","221132",
        "221231","213212","223112","312131","311222","321122","321221","312212","322112","322211",
        "212123","212321","232121","111323","131123","131321","112313","132113","132311","211313",
        "231113","231311","112133","112331","132131","113123","113321","133121","313121","211331",
        "231131","213113","213311","213131","311123","311321","331121","312113","312311","332111",
        "314111","221411","431111","111224","111422","121124","121421","141122","141221","112214",
        "112412","122114","122411","142112","142211","241211","221114","413111","241112","134111",
        "111242","121142","121241","114212","124112","124211","411212","421112","421211","212141",
        "214121","412121","111143","111341","131141","114113","114311","411113","411311","113141",
        "114131","311141","411131","211412","211214","211232","2331112"
    ];

    public static readonly BindableProperty CodeProperty = BindableProperty.Create(
        nameof(Code),
        typeof(string),
        typeof(BarcodePreviewView),
        "",
        propertyChanged: static (bindable, _, _) => ((BarcodePreviewView)bindable).Invalidate());

    public static readonly BindableProperty ProductNameProperty = PreviewProperty(nameof(ProductName), "");
    public static readonly BindableProperty PriceTextProperty = PreviewProperty(nameof(PriceText), "");
    public static readonly BindableProperty SkuProperty = PreviewProperty(nameof(Sku), "");
    public static readonly BindableProperty NameLinesProperty = PreviewProperty(nameof(NameLines), 2);

    public string Code
    {
        get => (string)GetValue(CodeProperty);
        set => SetValue(CodeProperty, value);
    }

    public string ProductName
    {
        get => (string)GetValue(ProductNameProperty);
        set => SetValue(ProductNameProperty, value);
    }

    public string PriceText
    {
        get => (string)GetValue(PriceTextProperty);
        set => SetValue(PriceTextProperty, value);
    }

    public string Sku
    {
        get => (string)GetValue(SkuProperty);
        set => SetValue(SkuProperty, value);
    }

    public int NameLines
    {
        get => (int)GetValue(NameLinesProperty);
        set => SetValue(NameLinesProperty, value);
    }

    public BarcodePreviewView() => Drawable = this;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FillColor = Colors.White;
        canvas.FillRectangle(dirtyRect);
        if (string.IsNullOrWhiteSpace(Code)) return;

        var values = Code.Select(c => c is >= ' ' and <= '~' ? c - 32 : '?' - 32).ToList();
        var checksum = 104;
        for (var i = 0; i < values.Count; i++) checksum += values[i] * (i + 1);
        values.Insert(0, 104);
        values.Add(checksum % 103);
        values.Add(106);

        var modules = values.Sum(value => Patterns[value].Sum(c => c - '0'));
        var content = dirtyRect.Inflate(-12, -8);
        var previewNameLines = NameLines <= 0 ? 3 : NameLines;
        var nameHeight = previewNameLines == 1 ? 16f : previewNameLines == 2 ? 30f : 44f;
        DrawName(canvas, ProductName, new RectF(content.Left, content.Top, content.Width, nameHeight), previewNameLines);
        var y = content.Top + nameHeight;
        canvas.FontColor = Colors.Black;
        canvas.Font = Microsoft.Maui.Graphics.Font.Default;
        canvas.FontSize = 9;
        if (!string.IsNullOrWhiteSpace(Sku))
        {
            canvas.DrawString($"SKU: {Sku}", content.Left, y, content.Width, 13, HorizontalAlignment.Center, VerticalAlignment.Center);
            y += 14;
        }
        if (!string.IsNullOrWhiteSpace(PriceText))
        {
            canvas.Font = Microsoft.Maui.Graphics.Font.DefaultBold;
            canvas.FontSize = 13;
            canvas.DrawString(PriceText, content.Left, y, content.Width, 18, HorizontalAlignment.Center, VerticalAlignment.Center);
            y += 19;
        }
        var barcodeTop = y + 2;
        var barcodeHeight = Math.Max(26f, content.Bottom - barcodeTop - 15);
        var quiet = 12f;
        var width = Math.Max(0.7f, (content.Width - quiet * 2) / modules);
        var x = content.Left + quiet;
        canvas.FillColor = Colors.Black;

        foreach (var value in values)
        {
            var pattern = Patterns[value];
            for (var i = 0; i < pattern.Length; i++)
            {
                var segment = (pattern[i] - '0') * width;
                if (i % 2 == 0) canvas.FillRectangle(x, barcodeTop, segment, barcodeHeight);
                x += segment;
            }
        }

        canvas.Font = Microsoft.Maui.Graphics.Font.Default;
        canvas.FontSize = 10;
        canvas.DrawString(Code, content.Left, barcodeTop + barcodeHeight, content.Width, 18, HorizontalAlignment.Center, VerticalAlignment.Center);
    }

    private static BindableProperty PreviewProperty<T>(string name, T defaultValue) => BindableProperty.Create(
        name,
        typeof(T),
        typeof(BarcodePreviewView),
        defaultValue,
        propertyChanged: static (bindable, _, _) => ((BarcodePreviewView)bindable).Invalidate());

    private static void DrawName(ICanvas canvas, string value, RectF bounds, int lines)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        canvas.Font = Microsoft.Maui.Graphics.Font.DefaultBold;
        canvas.FontColor = Colors.Black;
        canvas.FontSize = 12;
        var maxLines = Math.Clamp(lines, 1, 3);
        var maxCharacters = Math.Max(12, (int)(bounds.Width / 6.5f));
        var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var rows = new List<string>();
        var current = string.Empty;
        foreach (var word in words)
        {
            var candidate = string.IsNullOrEmpty(current) ? word : $"{current} {word}";
            if (candidate.Length <= maxCharacters)
            {
                current = candidate;
                continue;
            }
            if (!string.IsNullOrEmpty(current)) rows.Add(current);
            current = word;
            if (rows.Count == maxLines - 1) break;
        }
        if (!string.IsNullOrEmpty(current) && rows.Count < maxLines) rows.Add(current);
        if (rows.Count == 0) rows.Add(value);
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            if (row.Length > maxCharacters) row = $"{row[..Math.Max(1, maxCharacters - 1)]}…";
            canvas.DrawString(row, bounds.Left, bounds.Top + index * 16, bounds.Width, 16, HorizontalAlignment.Center, VerticalAlignment.Center);
        }
    }
}
