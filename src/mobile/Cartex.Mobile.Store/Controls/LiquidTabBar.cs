using Cartex.Mobile.Core;

namespace Cartex.Mobile.Store.Controls;

public class LiquidTabBar : Grid
{
    private const float BarTop = 29f;
    private const float BarHeight = 62f;
    private const float Dip = 30f;
    private const float NotchRadius = 36f;
    private const double DropletSize = 54;

    private static int _travelFrom = -1;

    private static readonly (string Route, string Glyph, string Key)[] Tabs =
    [
        ("home", "\U000F02DC", "tab_home"),
        ("trade", "\U000F02DA", "tab_trade"),
        ("scan", "\U000F0433", "scan"),
        ("customers", "\U000F0849", "tab_customer"),
        ("profile", "\U000F0004", "settings"),
    ];

    private readonly BarDrawable _drawable = new();
    private readonly GraphicsView _canvas;
    private readonly Border _droplet;
    private readonly Label _dropletIcon;
    private readonly VerticalStackLayout[] _items = new VerticalStackLayout[Tabs.Length];
    private double _center = -1;

    public int Index { get; set; }

    public LiquidTabBar()
    {
        HeightRequest = BarTop + BarHeight;
        Margin = new Thickness(14, 0, 14, 12);
        VerticalOptions = LayoutOptions.End;

        _canvas = new GraphicsView { Drawable = _drawable, InputTransparent = true };
        Children.Add(_canvas);

        var zones = new Grid
        {
            Margin = new Thickness(0, BarTop, 0, 0),
            HeightRequest = BarHeight,
            VerticalOptions = LayoutOptions.End,
        };
        for (var i = 0; i < Tabs.Length; i++)
        {
            zones.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
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
            zones.Add(zone, captured);
        }
        Children.Add(zones);

        _dropletIcon = new Label
        {
            FontFamily = "MDI",
            FontSize = 25,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
        };
        _dropletIcon.SetAppThemeColor(Label.TextColorProperty, Colors.White, Color.FromArgb("#052E16"));
        _droplet = new Border
        {
            WidthRequest = DropletSize,
            HeightRequest = DropletSize,
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = DropletSize / 2 },
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
            Content = _dropletIcon,
            Shadow = new Shadow { Brush = Brush.Black, Opacity = 0.35f, Radius = 12, Offset = new Point(0, 5) },
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
    }

    private void ApplyTheme()
    {
        var dark = Application.Current?.RequestedTheme == AppTheme.Dark;
        _drawable.Dark = dark;
        _droplet.Background = new LinearGradientBrush(
        [
            new GradientStop(dark ? Color.FromArgb("#4ADE80") : Color.FromArgb("#2F7A4C"), 0f),
            new GradientStop(dark ? Color.FromArgb("#22A85B") : Color.FromArgb("#1C4C2F"), 1f),
        ], new Point(0, 0), new Point(0, 1));
        _droplet.Stroke = new SolidColorBrush(dark ? Color.FromArgb("#66FFFFFF") : Color.FromArgb("#4DFFFFFF"));
        _canvas.Invalidate();
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0) return;
        _dropletIcon.Text = Tabs[Index].Glyph;
        _items[Index].Opacity = 0;
        if (_travelFrom >= 0 && _travelFrom != Index)
        {
            Apply(CenterFor(_travelFrom, width));
            _travelFrom = -1;
            SlideTo(CenterFor(Index, width));
        }
        else if (!this.AnimationIsRunning("slide"))
        {
            _travelFrom = -1;
            Apply(CenterFor(Index, width));
        }
    }

    private static double CenterFor(int index, double width) => width / Tabs.Length * (index + 0.5);

    private void Apply(double cx)
    {
        _center = cx;
        _drawable.NotchX = (float)cx;
        _canvas.Invalidate();
        _droplet.TranslationX = cx - DropletSize / 2;
    }

    private void SlideTo(double target)
    {
        this.AbortAnimation("slide");
        var start = _center;
        new Animation(v => Apply(v), start, target).Commit(this, "slide", 16, 380, Easing.SpringOut);
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
        public bool Dark;

        public void Draw(ICanvas canvas, RectF rect)
        {
            const float r = 31f;
            var top = BarTop;
            var bottom = BarTop + BarHeight;
            var w = rect.Width;
            var cx = Math.Clamp(NotchX, r + NotchRadius, w - r - NotchRadius);

            var p = new PathF();
            p.MoveTo(r, top);
            p.LineTo(cx - NotchRadius, top);
            p.CurveTo(cx - NotchRadius * 0.45f, top, cx - NotchRadius * 0.65f, top + Dip, cx, top + Dip);
            p.CurveTo(cx + NotchRadius * 0.65f, top + Dip, cx + NotchRadius * 0.45f, top, cx + NotchRadius, top);
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
            canvas.SetShadow(new SizeF(0, 6), 18, Color.FromRgba(0, 0, 0, Dark ? 0.55f : 0.18f));
            canvas.FillColor = Dark ? Color.FromArgb("#EE191E1B") : Color.FromArgb("#F2FFFFFF");
            canvas.FillPath(p);
            canvas.RestoreState();

            canvas.StrokeSize = 1.2f;
            canvas.StrokeColor = Dark ? Color.FromArgb("#2EFFFFFF") : Color.FromArgb("#D9FFFFFF");
            canvas.DrawPath(p);
        }
    }
}
