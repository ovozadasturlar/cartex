namespace Cartex.Mobile.Store.Controls;

public class LineChartView : GraphicsView
{
    public static readonly BindableProperty ValuesProperty = BindableProperty.Create(
        nameof(Values), typeof(IList<float>), typeof(LineChartView),
        propertyChanged: (b, _, v) => ((LineChartView)b).OnValuesChanged((IList<float>?)v));

    public IList<float>? Values
    {
        get => (IList<float>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    private readonly ChartDrawable _drawable = new();

    public LineChartView()
    {
        Drawable = _drawable;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        if (Application.Current is { } app)
        {
            app.RequestedThemeChanged -= OnThemeChanged;
            app.RequestedThemeChanged += OnThemeChanged;
        }
        Invalidate();
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        if (Application.Current is { } app)
            app.RequestedThemeChanged -= OnThemeChanged;
    }

    private void OnThemeChanged(object? sender, AppThemeChangedEventArgs e) => Invalidate();

    private void OnValuesChanged(IList<float>? values)
    {
        _drawable.Target = values is { Count: > 0 } ? [.. values] : [];
        AnimateIn();
    }

    private void AnimateIn()
    {
        this.AbortAnimation("grow");
        new Animation(v =>
        {
            _drawable.Progress = (float)v;
            Invalidate();
        }, 0, 1).Commit(this, "grow", 16, 650, Easing.CubicOut);
    }

    private sealed class ChartDrawable : IDrawable
    {
        public float[] Target = [];
        public float Progress = 1f;

        public void Draw(ICanvas canvas, RectF rect)
        {
            if (Target.Length < 2) return;
            var dark = Application.Current?.RequestedTheme == AppTheme.Dark;
            var line = dark ? Color.FromArgb("#4ADE80") : Color.FromArgb("#2F7A4C");

            const float padTop = 10f;
            const float padBottom = 6f;
            var w = rect.Width;
            var h = rect.Height;
            var max = Target.Max();
            if (max <= 0) max = 1;
            var stepX = w / Target.Length;

            var pts = new PointF[Target.Length];
            for (var i = 0; i < Target.Length; i++)
            {
                var v = Target[i] * Progress;
                var y = h - padBottom - (h - padTop - padBottom) * (v / max);
                pts[i] = new PointF((i + 0.5f) * stepX, y);
            }

            var curve = new PathF();
            curve.MoveTo(pts[0]);
            for (var i = 0; i < pts.Length - 1; i++)
            {
                var p0 = pts[Math.Max(i - 1, 0)];
                var p1 = pts[i];
                var p2 = pts[i + 1];
                var p3 = pts[Math.Min(i + 2, pts.Length - 1)];
                curve.CurveTo(
                    p1.X + (p2.X - p0.X) / 6f, p1.Y + (p2.Y - p0.Y) / 6f,
                    p2.X - (p3.X - p1.X) / 6f, p2.Y - (p3.Y - p1.Y) / 6f,
                    p2.X, p2.Y);
            }

            var area = new PathF(curve);
            area.LineTo(pts[^1].X, h);
            area.LineTo(pts[0].X, h);
            area.Close();

            canvas.SaveState();
            canvas.SetFillPaint(new LinearGradientPaint
            {
                StartColor = line.WithAlpha(dark ? 0.32f : 0.22f),
                EndColor = line.WithAlpha(0f),
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
            }, new RectF(0, 0, w, h));
            canvas.FillPath(area);
            canvas.RestoreState();

            canvas.StrokeColor = line;
            canvas.StrokeSize = 2.5f;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.DrawPath(curve);

            var last = pts[^1];
            canvas.FillColor = dark ? Color.FromArgb("#161A17") : Colors.White;
            canvas.FillCircle(last, 5.5f);
            canvas.StrokeSize = 3f;
            canvas.DrawCircle(last, 5.5f);
        }
    }
}
