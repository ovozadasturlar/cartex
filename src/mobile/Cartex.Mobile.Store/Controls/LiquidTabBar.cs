using Cartex.Mobile.Core;

namespace Cartex.Mobile.Store.Controls;

public class LiquidTabBar : Grid
{
    private const float BarTop = 29f;
    private const float BarHeight = 62f;
    private const float CornerRadius = 18f;
    private const float DropletSize = 50f;
    private const float RaisedTop = 19f;
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
        // Yangi sahifaning birinchi kadri tizim panellari inseti qo'llanishidan oldin
        // chiziladi va panel bir-ikki kadr pastga tushib ketadi. Shu kadrlarda panel
        // ko'rinmaydi, o'lcham joyiga tushgach yumshoq paydo bo'ladi.
        Opacity = 0;

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
        var travelling = from >= 0 && from != index
            && Interlocked.CompareExchange(ref _travelFrom, -1, from) == from;
        SetSelected(travelling ? from : index, 1f);

        Dispatcher.Dispatch(() =>
        {
            _ = this.FadeTo(1, 120, Easing.CubicOut);
            if (travelling) _ = AnimateAsync(from, index);
        });
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        if (Application.Current is { } app)
            app.RequestedThemeChanged -= OnThemeChanged;
        Loc.Instance.PropertyChanged -= OnLanguageChanged;
        this.AbortAnimation("liquid");
        this.CancelAnimations();
        _animating = false;
        Opacity = 0;
    }


    private static int CurrentSection()
    {
        var item = Shell.Current?.CurrentItem;
        if (item is null || item.Items.Count == 0) return 0;
        return Math.Max(0, item.Items.IndexOf(item.CurrentItem));
    }

    private int ResolvedIndex => Index >= 0 ? Index : CurrentSection();

    private static readonly Color MutedLight = Color.FromArgb("#6B7280");
    private static readonly Color MutedDark = Color.FromArgb("#9CA3AF");
    private static readonly Color OnDropletDark = Color.FromArgb("#052E16");

    private static Color Blend(Color from, Color to, float t) => new(
        from.Red + (to.Red - from.Red) * t,
        from.Green + (to.Green - from.Green) * t,
        from.Blue + (to.Blue - from.Blue) * t);

    /// `position` — tomchining ustunlar bo'yicha kasrli o'rni, shuning uchun u ikki tab
    /// orasida ham tura oladi va bir butun harakat sifatida suriladi.
    private void Apply(float position, float presence)
    {
        _drawable.Position = position;
        _drawable.Presence = presence;
        var dark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var muted = dark ? MutedDark : MutedLight;
        var onDroplet = dark ? OnDropletDark : Colors.White;
        var lift = IconLift;
        var raised = Math.Clamp(presence, 0f, 1f);
        for (var i = 0; i < TabCount; i++)
        {
            var weight = Math.Max(0f, 1f - Math.Abs(i - position));
            var strength = weight * raised;
            var opacity = 1 - strength;
            var translation = -lift * strength;
            var scale = 1 + 0.16 * strength;
            var color = Blend(muted, onDroplet, Math.Clamp((strength - 0.35f) / 0.35f, 0f, 1f));
            if (_labels[i].Opacity != opacity) _labels[i].Opacity = opacity;
            if (_icons[i].TranslationY != translation) _icons[i].TranslationY = translation;
            if (_icons[i].Scale != scale) _icons[i].Scale = scale;
            if (!Equals(_icons[i].TextColor, color)) _icons[i].TextColor = color;
        }
        _canvas.Invalidate();
    }

    private void SetSelected(int index, float presence) => Apply(index, presence);

    private const uint TravelMs = 460;
    private const float DipDepth = 0.94f;

    private async Task AnimateAsync(int from, int to)
    {
        _animating = true;
        try
        {
            var done = new TaskCompletionSource();
            new Animation(v =>
                {
                    var t = (float)v;
                    var slide = (float)Easing.CubicInOut.Ease(t);
                    // Botish tez, sirpanish davomida past, chiqish esa yengil sakrash bilan —
                    // shunda tomchi ikki alohida harakat emas, bitta oqim bo'lib ko'rinadi.
                    var sink = (float)Easing.CubicOut.Ease(Math.Clamp(t / 0.26, 0, 1));
                    var rise = (float)Easing.SpringOut.Ease(Math.Clamp((t - 0.54) / 0.46, 0, 1));
                    Apply(from + (to - from) * slide, 1f - DipDepth * (sink - rise));
                }, 0, 1)
                .Commit(this, "liquid", 16, TravelMs, Easing.Linear, (_, _) => done.TrySetResult());
            await done.Task;
            Apply(to, 1f);
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
            await Shell.Current.GoToAsync($"//main/{Routes[index]}", animate: false);
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
        private static readonly LinearGradientPaint BarLight = new()
        {
            StartColor = Color.FromArgb("#FCFFFFFF"),
            EndColor = Color.FromArgb("#E9EDF4EF"),
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
        };

        private static readonly LinearGradientPaint BarDark = new()
        {
            StartColor = Color.FromArgb("#F7242B27"),
            EndColor = Color.FromArgb("#F7151A17"),
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
        };

        private static readonly LinearGradientPaint DropletLight = new()
        {
            StartColor = Color.FromArgb("#348354"),
            EndColor = Color.FromArgb("#1A4A2D"),
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
        };

        private static readonly LinearGradientPaint DropletDark = new()
        {
            StartColor = Color.FromArgb("#52E68C"),
            EndColor = Color.FromArgb("#1E9C54"),
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
        };

        public float Position;
        public float Presence = 1f;
        public float EdgePad;
        public bool Dark;

        public void Draw(ICanvas canvas, RectF rect)
        {
            var w = rect.Width;
            if (w <= 0) return;

            var o = Math.Clamp(Presence, 0f, 1f);
            var colW = (w - 2 * EdgePad) / TabCount;
            var cx = EdgePad + colW * (Position + 0.5f);
            var top = BarTop;
            var bottom = rect.Height;
            // Chiqishdagi sakrash uchun xom qiymat ishlatiladi: `o` faqat shakl va shaffoflikni
            // boshqaradi, tomchi esa o'z joyidan bir oz yuqoriga chiqib qaytishi mumkin.
            var dropletCy = RaisedTop + DropletSize / 2 + (SunkenTop - RaisedTop) * (1 - Presence);

            var path = BuildPath(w, top, bottom, cx, dropletCy, o);

            canvas.SaveState();
            canvas.SetShadow(new SizeF(0, -4), 16, Color.FromRgba(0, 0, 0, Dark ? 0.5f : 0.16f));
            canvas.SetFillPaint(Dark ? BarDark : BarLight, new RectF(0, top, w, bottom - top));
            canvas.FillPath(path);
            canvas.RestoreState();

            if (o <= 0.02f) return;
            var r = DropletSize / 2f;
            // Botayotganda tomchi bir oz yassilanadi — suyuqlikning cho'zilishi shu bilan seziladi.
            var squash = 1f + 0.18f * (1f - o);
            var rx = r * squash;
            var ry = r / squash;
            canvas.SaveState();
            canvas.SetShadow(new SizeF(0, 4), 10, Color.FromRgba(0, 0, 0, 0.35f * o));
            canvas.SetFillPaint(Dark ? DropletDark : DropletLight, new RectF(cx - rx, dropletCy - ry, rx * 2, ry * 2));
            canvas.FillEllipse(cx - rx, dropletCy - ry, rx * 2, ry * 2);
            canvas.RestoreState();

            canvas.StrokeSize = 1f;
            canvas.StrokeColor = Color.FromRgba(1f, 1f, 1f, 0.4f * o);
            canvas.DrawEllipse(cx - rx, dropletCy - ry, rx * 2, ry * 2);
            canvas.FillColor = Color.FromRgba(1f, 1f, 1f, 0.3f * o * o);
            canvas.FillRoundedRectangle(cx - rx + 10, dropletCy - ry + 6, 15, 8, 4);
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
