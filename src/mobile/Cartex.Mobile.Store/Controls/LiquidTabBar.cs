using Cartex.Mobile.Core;

namespace Cartex.Mobile.Store.Controls;

public class LiquidTabBar : Grid
{
    private const float BarTop = 29f;
    private const float BarHeight = 62f;
    private const float CornerRadius = 18f;
    private const float DropletSize = 50f;
    private const float RaisedTop = 13f;
    private const float SunkenTop = BarTop + 8f;
    private const float Gap = 9f;
    private const float CradleRadius = DropletSize / 2 + Gap;
    private const float LipRun = 8f;
    private const int TabCount = 5;
    private const float DropletCenterY = RaisedTop + DropletSize / 2;

    private static readonly float HalfWFull =
        (float)Math.Sqrt(CradleRadius * CradleRadius - (BarTop - (RaisedTop + DropletSize / 2)) * (BarTop - (RaisedTop + DropletSize / 2)));

    private static readonly string[] Keys = ["tab_home", "tab_trade", "scan", "tab_customer", "tab_profile"];

    private static int _travelFrom = -1;

    private readonly BarDrawable _drawable = new();
    private readonly GraphicsView _canvas;
    private readonly Grid _zones;
    private readonly Label[] _labels = new Label[TabCount];
    private readonly Label[] _icons = new Label[TabCount];
    private bool _animating;

    private double IconLift
    {
        get
        {
            var icon = _icons[0];
            if (icon.Height <= 0) return 13;
            return BarTop + icon.Y + icon.Height / 2 - DropletCenterY;
        }
    }

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
        for (var i = 0; i < TabCount; i++)
        {
            _zones.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var icon = new Label
            {
                FontFamily = "MDI",
                FontSize = 23,
                Text = Glyphs[i],
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Start,
                Margin = new Thickness(0, 8, 0, 0),
                InputTransparent = true,
            };
            icon.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#6B7280"), Color.FromArgb("#9CA3AF"));
            _icons[i] = icon;
            var text = new Label
            {
                FontSize = 10.5,
                FontFamily = "OpenSansSemibold",
                Text = Loc.Instance[Keys[i]],
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.End,
                Margin = new Thickness(0, 0, 0, 9),
                LineBreakMode = LineBreakMode.TailTruncation,
                InputTransparent = true,
            };
            text.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#6B7280"), Color.FromArgb("#9CA3AF"));
            _labels[i] = text;
            var zone = new Grid { BackgroundColor = Colors.Transparent, Children = { icon, text } };
            var tap = new TapGestureRecognizer();
            var captured = i;
            tap.Tapped += (_, _) => OnTap(captured);
            zone.GestureRecognizers.Add(tap);
            _zones.Add(zone, captured);
        }
        Children.Add(_zones);

        ApplyTheme();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnThemeChanged(object? sender, AppThemeChangedEventArgs e) => ApplyTheme();

    private void OnLanguageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        for (var i = 0; i < TabCount; i++)
            _labels[i].Text = Loc.Instance[Keys[i]];
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        if (Application.Current is { } app)
        {
            app.RequestedThemeChanged -= OnThemeChanged;
            app.RequestedThemeChanged += OnThemeChanged;
        }
        Loc.Instance.PropertyChanged -= OnLanguageChanged;
        Loc.Instance.PropertyChanged += OnLanguageChanged;
        ApplyTheme();
        OnLanguageChanged(null, new System.ComponentModel.PropertyChangedEventArgs("Item[]"));

        var index = ResolvedIndex;
        var from = _travelFrom;
        if (from >= 0 && from != index && Interlocked.CompareExchange(ref _travelFrom, -1, from) == from)
        {
            SetSelected(from, 1f);
            _ = AnimateAsync(from, index);
        }
        else
        {
            SetSelected(index, 1f);
        }
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        if (Application.Current is { } app)
            app.RequestedThemeChanged -= OnThemeChanged;
        Loc.Instance.PropertyChanged -= OnLanguageChanged;
        this.AbortAnimation("liquid");
        _animating = false;
    }


    private static int CurrentSection()
    {
        var item = Shell.Current?.CurrentItem;
        if (item is null || item.Items.Count == 0) return 0;
        return Math.Max(0, item.Items.IndexOf(item.CurrentItem));
    }

    private int ResolvedIndex => Index >= 0 ? Index : CurrentSection();

    private void SetSelected(int index, float presence)
    {
        _drawable.SelectedIndex = index;
        _drawable.Presence = presence;
        var dark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var muted = dark ? Color.FromArgb("#9CA3AF") : Color.FromArgb("#6B7280");
        var onDroplet = dark ? Color.FromArgb("#052E16") : Colors.White;
        var lift = IconLift;
        for (var i = 0; i < TabCount; i++)
        {
            var selected = i == index;
            _labels[i].Opacity = selected ? 1 - presence : 1;
            _icons[i].TranslationY = selected ? -lift * presence : 0;
            _icons[i].Scale = selected ? 1 + 0.18 * presence : 1;
            _icons[i].TextColor = selected && presence > 0.5f ? onDroplet : muted;
        }
        _canvas.Invalidate();
    }

    private async Task AnimateAsync(int from, int to)
    {
        _animating = true;
        try
        {
            var sink = new TaskCompletionSource();
            new Animation(v => SetSelected(from, 1 - (float)v), 0, 1)
                .Commit(this, "liquid", 16, 170, Easing.CubicIn, (_, _) => sink.TrySetResult());
            await sink.Task;

            var rise = new TaskCompletionSource();
            new Animation(v => SetSelected(to, (float)v), 0, 1)
                .Commit(this, "liquid", 16, 380, Easing.SpringOut, (_, _) => rise.TrySetResult());
            await rise.Task;
            SetSelected(to, 1f);
        }
        finally
        {
            _animating = false;
        }
    }

    private void ApplyTheme()
    {
        _drawable.Dark = Application.Current?.RequestedTheme == AppTheme.Dark;
        _canvas.Invalidate();
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0) return;
        var min = CornerRadius + HalfWFull + LipRun;
        var pad = Math.Max(0, (min - width / (TabCount * 2.0)) / (1 - 1.0 / TabCount));
        _zones.Padding = new Thickness(pad, 0);
        _drawable.EdgePad = (float)pad;
        if (!_animating)
            SetSelected(ResolvedIndex, 1f);
    }

    private async void OnTap(int index)
    {
        if (index == ResolvedIndex)
        {
            if (Shell.Current?.Navigation.NavigationStack.Count > 1)
                await Shell.Current.Navigation.PopToRootAsync();
            return;
        }
        _travelFrom = ResolvedIndex;
        try
        {
            await Shell.Current.GoToAsync($"//main/{Routes[index]}");
        }
        catch
        {
            _travelFrom = -1;
        }
    }

    private static readonly string[] Routes = ["home", "trade", "scan", "customers", "profile"];

    private static readonly string[] Glyphs =
        ["\U000F02DC", "\U000F02DA", "\U000F0433", "\U000F0849", "\U000F0004"];

    private sealed class BarDrawable : IDrawable
    {
        public int SelectedIndex;
        public float Presence = 1f;
        public float EdgePad;
        public bool Dark;

        public void Draw(ICanvas canvas, RectF rect)
        {
            var w = rect.Width;
            if (w <= 0) return;

            var o = Math.Clamp(Presence, 0f, 1f);
            var colW = (w - 2 * EdgePad) / TabCount;
            var cx = EdgePad + colW * (SelectedIndex + 0.5f);
            var top = BarTop;
            var bottom = rect.Height;
            var dropletCy = RaisedTop + DropletSize / 2 + (SunkenTop - RaisedTop) * (1 - o);

            var path = BuildPath(w, top, bottom, cx, dropletCy, o);

            canvas.SaveState();
            canvas.SetShadow(new SizeF(0, -4), 16, Color.FromRgba(0, 0, 0, Dark ? 0.5f : 0.16f));
            canvas.SetFillPaint(new LinearGradientPaint
            {
                StartColor = Dark ? Color.FromArgb("#F7242B27") : Color.FromArgb("#FCFFFFFF"),
                EndColor = Dark ? Color.FromArgb("#F7151A17") : Color.FromArgb("#E9EDF4EF"),
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
            }, new RectF(0, top, w, bottom - top));
            canvas.FillPath(path);
            canvas.RestoreState();

            if (o <= 0.02f) return;
            var r = DropletSize / 2f;
            canvas.SaveState();
            canvas.SetShadow(new SizeF(0, 4), 10, Color.FromRgba(0, 0, 0, 0.35f * o));
            canvas.SetFillPaint(new LinearGradientPaint
            {
                StartColor = Dark ? Color.FromArgb("#52E68C") : Color.FromArgb("#348354"),
                EndColor = Dark ? Color.FromArgb("#1E9C54") : Color.FromArgb("#1A4A2D"),
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
            }, new RectF(cx - r, dropletCy - r, DropletSize, DropletSize));
            canvas.FillCircle(cx, dropletCy, r);
            canvas.RestoreState();

            canvas.StrokeSize = 1f;
            canvas.StrokeColor = Color.FromRgba(1f, 1f, 1f, 0.4f * o);
            canvas.DrawCircle(cx, dropletCy, r);
            canvas.FillColor = Color.FromRgba(1f, 1f, 1f, 0.3f * o);
            canvas.FillRoundedRectangle(cx - r + 10, dropletCy - r + 6, 15, 8, 4);
        }

        private static PathF BuildPath(float w, float top, float bottom, float cx, float dropletCy, float o)
        {
            var p = new PathF();
            p.MoveTo(CornerRadius, top);
            var radius = CradleRadius * (0.35f + 0.65f * o);
            var dy = top - dropletCy;
            var span = radius * radius - dy * dy;
            if (o > 0.03f && span > 4f)
            {
                var halfW = (float)Math.Sqrt(span);
                var ncx = Math.Clamp(cx, CornerRadius + halfW + LipRun, w - CornerRadius - halfW - LipRun);
                var lip = 3.5f * o;
                p.LineTo(ncx - halfW - LipRun, top);
                p.CurveTo(ncx - halfW - LipRun * 0.35f, top, ncx - halfW - 1.5f, top - lip, ncx - halfW, top);
                var a0 = Math.Atan2(dy, -halfW);
                var a1 = Math.Atan2(dy, halfW) - 2 * Math.PI;
                const int steps = 26;
                for (var i = 1; i <= steps; i++)
                {
                    var a = a0 + (a1 - a0) * i / steps;
                    p.LineTo(ncx + (float)(radius * Math.Cos(a)), dropletCy + (float)(radius * Math.Sin(a)));
                }
                p.CurveTo(ncx + halfW + 1.5f, top - lip, ncx + halfW + LipRun * 0.35f, top, ncx + halfW + LipRun, top);
            }
            p.LineTo(w - CornerRadius, top);
            p.QuadTo(w, top, w, top + CornerRadius);
            p.LineTo(w, bottom);
            p.LineTo(0, bottom);
            p.LineTo(0, top + CornerRadius);
            p.QuadTo(0, top, CornerRadius, top);
            p.Close();
            return p;
        }

    }
}
