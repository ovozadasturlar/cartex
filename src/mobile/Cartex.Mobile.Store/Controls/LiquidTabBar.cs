using Cartex.Mobile.Core;

namespace Cartex.Mobile.Store.Controls;

public class LiquidTabBar : Grid
{
    private const float BarTop = 29f;
    private const float BarHeight = 62f;
    private const float CornerRadius = 18f;
    private const float DropletSize = 46f;
    private const float RaisedTop = 15f;
    private const float SunkenTop = BarTop + 10f;
    private const float Gap = 9f;
    private const float CradleRadius = DropletSize / 2 + Gap;
    private const float LipRun = 8f;

    private static int _travelFrom = -1;
    private static double _settledParentHeight = -1;
    private static readonly float HalfWFull =
        (float)Math.Sqrt(CradleRadius * CradleRadius - (BarTop - (RaisedTop + DropletSize / 2)) * (BarTop - (RaisedTop + DropletSize / 2)));

    private static readonly (string Route, string Glyph, string Key)[] Tabs =
    [
        ("home", "\U000F02DC", "tab_home"),
        ("trade", "\U000F02DA", "tab_trade"),
        ("scan", "\U000F0433", "scan"),
        ("customers", "\U000F0849", "tab_customer"),
        ("profile", "\U000F0004", "tab_profile"),
    ];

    private readonly BarDrawable _drawable = new();
    private readonly GraphicsView _canvas;
    private readonly Grid _zones;
    private readonly Border _droplet;
    private readonly Label _dropletIcon;
    private readonly VerticalStackLayout[] _items = new VerticalStackLayout[Tabs.Length];
    private double _edgePad;
    private bool _busy;

    public int Index { get; set; }

    public LiquidTabBar()
    {
        HeightRequest = BarTop + BarHeight;
        Margin = new Thickness(14, 0, 14, 12);
        VerticalOptions = LayoutOptions.End;
        Opacity = 0;

        _canvas = new GraphicsView { Drawable = _drawable, InputTransparent = true };
        Children.Add(_canvas);

        _zones = new Grid
        {
            Margin = new Thickness(0, BarTop, 0, 0),
            HeightRequest = BarHeight,
            VerticalOptions = LayoutOptions.End,
        };
        for (var i = 0; i < Tabs.Length; i++)
        {
            _zones.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var icon = new Label
            {
                FontFamily = "MDI",
                FontSize = 23,
                Text = Tabs[i].Glyph,
                HorizontalOptions = LayoutOptions.Center,
            };
            icon.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#6B7280"), Color.FromArgb("#9CA3AF"));
            var text = new Label
            {
                FontSize = 10.5,
                FontFamily = "OpenSansSemibold",
                Text = Loc.Instance[Tabs[i].Key],
                HorizontalOptions = LayoutOptions.Center,
            };
            text.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#6B7280"), Color.FromArgb("#9CA3AF"));
            var stack = new VerticalStackLayout
            {
                Spacing = 2,
                VerticalOptions = LayoutOptions.Center,
                Children = { icon, text },
            };
            _items[i] = stack;
            var zone = new Grid { BackgroundColor = Colors.Transparent, Children = { stack } };
            var tap = new TapGestureRecognizer();
            var captured = i;
            tap.Tapped += (_, _) => OnTap(captured);
            zone.GestureRecognizers.Add(tap);
            _zones.Add(zone, captured);
        }
        Children.Add(_zones);

        _dropletIcon = new Label
        {
            FontFamily = "MDI",
            FontSize = 21,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
        };
        _dropletIcon.SetAppThemeColor(Label.TextColorProperty, Colors.White, Color.FromArgb("#052E16"));
        var gloss = new Border
        {
            WidthRequest = 14,
            HeightRequest = 8,
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 4 },
            BackgroundColor = Color.FromArgb("#59FFFFFF"),
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(9, 5, 0, 0),
            InputTransparent = true,
        };
        _droplet = new Border
        {
            WidthRequest = DropletSize,
            HeightRequest = DropletSize,
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = DropletSize / 2 },
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
            Content = new Grid { Children = { gloss, _dropletIcon } },
            Shadow = new Shadow { Brush = Brush.Black, Opacity = 0.3f, Radius = 10, Offset = new Point(0, 4) },
        };
        ApplyTheme();
        Children.Add(_droplet);

        if (Application.Current is { } app)
            app.RequestedThemeChanged += (_, _) => ApplyTheme();
        Loc.Instance.PropertyChanged += (_, _) =>
        {
            for (var i = 0; i < Tabs.Length; i++)
                if (_items[i].Children[1] is Label lb)
                    lb.Text = Loc.Instance[Tabs[i].Key];
        };

        Loaded += (_, _) => OnAppear();
        Unloaded += (_, _) =>
        {
            this.AbortAnimation("liquid");
            _busy = false;
            Opacity = 0;
        };
    }

    private void ApplyTheme()
    {
        var dark = Application.Current?.RequestedTheme == AppTheme.Dark;
        _drawable.Dark = dark;
        _droplet.Background = new LinearGradientBrush(
        [
            new GradientStop(dark ? Color.FromArgb("#52E68C") : Color.FromArgb("#348354"), 0f),
            new GradientStop(dark ? Color.FromArgb("#1E9C54") : Color.FromArgb("#1A4A2D"), 1f),
        ], new Point(0, 0), new Point(0, 1));
        _droplet.Stroke = new SolidColorBrush(dark ? Color.FromArgb("#73FFFFFF") : Color.FromArgb("#66FFFFFF"));
        _canvas.Invalidate();
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0) return;
        var min = CornerRadius + HalfWFull + LipRun;
        _edgePad = Math.Max(0, (min - width / (Tabs.Length * 2.0)) / (1 - 1.0 / Tabs.Length));
        _zones.Padding = new Thickness(_edgePad, 0);
        if (!_busy && !this.AnimationIsRunning("liquid"))
            Apply(CenterFor(Index, width), 1, 1);
    }

    private void OnAppear()
    {
        if (_busy) return;
        _busy = true;
        _ = EnterAsync();
    }

    private async Task EnterAsync()
    {
        try
        {
            for (var i = 0; i < 40 && Width <= 0; i++)
                await Task.Delay(25);
            if (Width <= 0)
            {
                Opacity = 1;
                return;
            }

            var from = _travelFrom;
            _travelFrom = -1;
            var travelling = from >= 0 && from != Index;
            SetDroplet(travelling ? from : Index);
            Apply(CenterFor(travelling ? from : Index, Width), 1, 1);

            double ParentHeight() => (Parent as VisualElement)?.Height ?? -1;
            if (_settledParentHeight > 0)
            {
                for (var i = 0; i < 18 && Math.Abs(ParentHeight() - _settledParentHeight) >= 1; i++)
                    await Task.Delay(50);
            }
            else
            {
                var last = double.NaN;
                for (var i = 0; i < 18; i++)
                {
                    await Task.Delay(50);
                    var h = ParentHeight();
                    if (!double.IsNaN(last) && Math.Abs(h - last) < 0.5) break;
                    last = h;
                }
            }
            _settledParentHeight = ParentHeight();

            await this.FadeTo(1, 130, Easing.CubicOut);
            if (travelling)
                await AnimateSwitchAsync(from, Width);
        }
        finally
        {
            _busy = false;
        }
    }

    private void SetDroplet(int index)
    {
        _dropletIcon.Text = Tabs[index].Glyph;
        for (var i = 0; i < Tabs.Length; i++)
            _items[i].Opacity = i == index ? 0 : 1;
    }

    private async Task AnimateSwitchAsync(int from, double width)
    {
        var fromX = CenterFor(from, width);
        var toX = CenterFor(Index, width);

        var sink = new TaskCompletionSource();
        new Animation(v => Apply(fromX, 1 - v, 1 - v), 0, 1)
            .Commit(this, "liquid", 16, 240, Easing.CubicIn, (_, _) => sink.TrySetResult());
        await sink.Task;

        SetDroplet(Index);
        _items[from].Opacity = 0;
        _ = _items[from].FadeTo(1, 160);
        _items[Index].Opacity = 0;

        var rise = new TaskCompletionSource();
        new Animation(v => Apply(toX, v, v), 0, 1)
            .Commit(this, "liquid", 16, 420, Easing.SpringOut, (_, _) => rise.TrySetResult());
        await rise.Task;
    }

    private double CenterFor(int index, double width) =>
        _edgePad + (width - 2 * _edgePad) / Tabs.Length * (index + 0.5);

    private void Apply(double cx, double openness, double presence)
    {
        var shown = Math.Clamp(presence, 0, 1);
        _drawable.NotchX = (float)cx;
        _drawable.Openness = (float)Math.Clamp(openness, 0, 1);
        _canvas.Invalidate();
        _droplet.TranslationX = cx - DropletSize / 2;
        _droplet.TranslationY = RaisedTop + (SunkenTop - RaisedTop) * (1 - openness);
        _droplet.Opacity = shown;
        _droplet.Scale = 0.8 + 0.2 * shown;
    }

    private async void OnTap(int index)
    {
        if (index == Index) return;
        _travelFrom = Index;
        try
        {
            await Shell.Current.GoToAsync($"//main/{Tabs[index].Route}");
        }
        catch
        {
        }
    }

    private sealed class BarDrawable : IDrawable
    {
        public float NotchX;
        public float Openness = 1f;
        public bool Dark;

        public void Draw(ICanvas canvas, RectF rect)
        {
            const float r = CornerRadius;
            var top = BarTop;
            var bottom = BarTop + BarHeight;
            var w = rect.Width;
            var o = Openness;

            var p = new PathF();
            p.MoveTo(r, top);
            if (o > 0.03f)
            {
                var cy = RaisedTop + DropletSize / 2 + (SunkenTop - RaisedTop) * (1 - o);
                var radius = CradleRadius * (0.35f + 0.65f * o);
                var dy = top - cy;
                var span = radius * radius - dy * dy;
                if (span > 4f)
                {
                    var halfW = (float)Math.Sqrt(span);
                    var cx = Math.Clamp(NotchX, r + halfW + LipRun, w - r - halfW - LipRun);
                    var lift = 3.5f * o;

                    p.LineTo(cx - halfW - LipRun, top);
                    p.CurveTo(cx - halfW - LipRun * 0.35f, top, cx - halfW - 1.5f, top - lift, cx - halfW, top);
                    var a0 = Math.Atan2(dy, -halfW);
                    var a1 = Math.Atan2(dy, halfW) - 2 * Math.PI;
                    const int steps = 26;
                    for (var i = 1; i <= steps; i++)
                    {
                        var a = a0 + (a1 - a0) * i / steps;
                        p.LineTo(cx + (float)(radius * Math.Cos(a)), cy + (float)(radius * Math.Sin(a)));
                    }
                    p.CurveTo(cx + halfW + 1.5f, top - lift, cx + halfW + LipRun * 0.35f, top, cx + halfW + LipRun, top);
                }
            }
            p.LineTo(w - r, top);
            p.QuadTo(w, top, w, top + r);
            p.LineTo(w, bottom - r);
            p.QuadTo(w, bottom, w - r, bottom);
            p.LineTo(r, bottom);
            p.QuadTo(0, bottom, 0, bottom - r);
            p.LineTo(0, top + r);
            p.QuadTo(0, top, r, top);
            p.Close();

            canvas.SaveState();
            canvas.SetShadow(new SizeF(0, 6), 18, Color.FromRgba(0, 0, 0, Dark ? 0.55f : 0.2f));
            canvas.SetFillPaint(new LinearGradientPaint
            {
                StartColor = Dark ? Color.FromArgb("#F2242B27") : Color.FromArgb("#FCFFFFFF"),
                EndColor = Dark ? Color.FromArgb("#F2151A17") : Color.FromArgb("#E4EDF4EF"),
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
            }, new RectF(0, top, w, BarHeight));
            canvas.FillPath(p);
            canvas.RestoreState();
        }
    }
}
