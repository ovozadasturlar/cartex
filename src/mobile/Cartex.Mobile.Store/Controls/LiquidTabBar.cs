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
    private static readonly List<WeakReference<LiquidTabBar>> Registry = [];
    private static readonly float HalfWFull =
        (float)Math.Sqrt(CradleRadius * CradleRadius - (BarTop - (RaisedTop + DropletSize / 2)) * (BarTop - (RaisedTop + DropletSize / 2)));

    private static readonly (string Glyph, string Key)[] Tabs =
    [
        ("\U000F02DC", "tab_home"),
        ("\U000F02DA", "tab_trade"),
        ("\U000F0433", "scan"),
        ("\U000F0849", "tab_customer"),
        ("\U000F0004", "tab_profile"),
    ];

    private readonly BarDrawable _drawable = new();
    private readonly GraphicsView _canvas;
    private readonly Grid _zones;
    private readonly Border _droplet;
    private readonly Label _dropletIcon;
    private readonly VerticalStackLayout[] _items = new VerticalStackLayout[Tabs.Length];
    private double _edgePad;
    private int _shownIndex;
    private bool _animating;

    public int Index { get; set; } = -1;

    public LiquidTabBar()
    {
        HeightRequest = BarTop + BarHeight;
        VerticalOptions = LayoutOptions.End;

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

        Registry.Add(new WeakReference<LiquidTabBar>(this));

        if (Application.Current is { } app)
            app.RequestedThemeChanged += (_, _) => ApplyTheme();
        Loc.Instance.PropertyChanged += (_, _) =>
        {
            for (var i = 0; i < Tabs.Length; i++)
                if (_items[i].Children[1] is Label lb)
                    lb.Text = Loc.Instance[Tabs[i].Key];
        };

        Loaded += (_, _) =>
        {
#if ANDROID
            if (Handler?.PlatformView is Android.Views.View pv && pv.RootView is { } root)
                AndroidX.Core.View.ViewCompat.RequestApplyInsets(root);
#endif
            Activate();
        };
        Unloaded += (_, _) =>
        {
            this.AbortAnimation("liquid");
            _animating = false;
        };
    }

    private static int CurrentSection()
    {
        var item = Shell.Current?.CurrentItem;
        if (item is null || item.Items.Count == 0) return 0;
        return Math.Max(0, item.Items.IndexOf(item.CurrentItem));
    }

    private int ResolvedIndex => Index >= 0 ? Index : CurrentSection();

    private static void ActivateVisible()
    {
        for (var i = Registry.Count - 1; i >= 0; i--)
        {
            if (!Registry[i].TryGetTarget(out var bar))
            {
                Registry.RemoveAt(i);
                continue;
            }
            if (bar.Window is not null)
                bar.Activate();
        }
    }

    private void Activate()
    {
        if (_animating) return;
        _ = ActivateAsync();
    }

    private async Task ActivateAsync()
    {
        _animating = true;
        try
        {
            for (var i = 0; i < 40 && Width <= 0; i++)
                await Task.Delay(25);
            if (Width <= 0) return;

            _shownIndex = ResolvedIndex;
            var from = _travelFrom;
            if (from >= 0 && from != _shownIndex &&
                Interlocked.CompareExchange(ref _travelFrom, -1, from) == from)
            {
                SetDroplet(from);
                Apply(CenterFor(from, Width), 1, 1);
                await AnimateSwitchAsync(from);
            }
            else
            {
                SetDroplet(_shownIndex);
                Apply(CenterFor(_shownIndex, Width), 1, 1);
            }
        }
        finally
        {
            _animating = false;
        }
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
        if (!_animating && !this.AnimationIsRunning("liquid"))
        {
            _shownIndex = ResolvedIndex;
            if (_travelFrom >= 0 && _travelFrom != _shownIndex) return;
            SetDroplet(_shownIndex);
            Apply(CenterFor(_shownIndex, width), 1, 1);
        }
    }

    private void SetDroplet(int index)
    {
        _dropletIcon.Text = Tabs[index].Glyph;
        for (var i = 0; i < Tabs.Length; i++)
            _items[i].Opacity = i == index ? 0 : 1;
    }

    private async Task AnimateSwitchAsync(int from)
    {
        var fromX = CenterFor(from, Width);
        var toX = CenterFor(_shownIndex, Width);

        var sink = new TaskCompletionSource();
        new Animation(v => Apply(fromX, 1 - v, 1 - v), 0, 1)
            .Commit(this, "liquid", 16, 220, Easing.CubicIn, (_, _) => sink.TrySetResult());
        await sink.Task;

        SetDroplet(_shownIndex);
        _items[from].Opacity = 0;
        _ = _items[from].FadeTo(1, 160);
        _items[_shownIndex].Opacity = 0;

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
        try
        {
            var shell = Shell.Current;
            var tabBar = shell?.CurrentItem;
            if (shell is null || tabBar is null || tabBar.Items.Count <= index) return;
            var current = CurrentSection();
            if (index == current)
            {
                if (shell.Navigation.NavigationStack.Count > 1)
                    await shell.Navigation.PopToRootAsync();
                return;
            }
            _travelFrom = current;
            tabBar.CurrentItem = tabBar.Items[index];
            await Task.Delay(40);
            ActivateVisible();
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
            p.LineTo(w, bottom);
            p.LineTo(0, bottom);
            p.LineTo(0, top + r);
            p.QuadTo(0, top, r, top);
            p.Close();

            canvas.SaveState();
            canvas.SetShadow(new SizeF(0, -4), 16, Color.FromRgba(0, 0, 0, Dark ? 0.5f : 0.16f));
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
