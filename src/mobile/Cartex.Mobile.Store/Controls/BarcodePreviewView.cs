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

    public string Code
    {
        get => (string)GetValue(CodeProperty);
        set => SetValue(CodeProperty, value);
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
        var quiet = 20f;
        var width = Math.Max(1f, (dirtyRect.Width - quiet * 2) / modules);
        var x = dirtyRect.Left + quiet;
        var height = Math.Max(1f, dirtyRect.Height - 28f);
        canvas.FillColor = Colors.Black;

        foreach (var value in values)
        {
            var pattern = Patterns[value];
            for (var i = 0; i < pattern.Length; i++)
            {
                var segment = (pattern[i] - '0') * width;
                if (i % 2 == 0) canvas.FillRectangle(x, dirtyRect.Top + 4, segment, height);
                x += segment;
            }
        }

        canvas.FontColor = Colors.Black;
        canvas.FontSize = 12;
        canvas.DrawString(Code, dirtyRect.Left, dirtyRect.Bottom - 22, dirtyRect.Width, 20, HorizontalAlignment.Center, VerticalAlignment.Center);
    }
}
