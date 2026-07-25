namespace Cartex.Mobile.Core.Controls;

public sealed class Thumb : ContentView
{
    public static readonly BindableProperty SourceProperty =
        BindableProperty.Create(nameof(Source), typeof(string), typeof(Thumb), propertyChanged: OnChanged);

    public static readonly BindableProperty SizeProperty =
        BindableProperty.Create(nameof(Size), typeof(double), typeof(Thumb), 64d, propertyChanged: OnChanged);

    public static readonly BindableProperty RadiusProperty =
        BindableProperty.Create(nameof(Radius), typeof(double), typeof(Thumb), 12d, propertyChanged: OnChanged);

    public static readonly BindableProperty GlyphProperty =
        BindableProperty.Create(nameof(Glyph), typeof(string), typeof(Thumb), "\U000f03d7", propertyChanged: OnChanged);

    public static readonly BindableProperty StretchProperty =
        BindableProperty.Create(nameof(Stretch), typeof(bool), typeof(Thumb), false, propertyChanged: OnChanged);

    public string? Source { get => (string?)GetValue(SourceProperty); set => SetValue(SourceProperty, value); }
    public double Size { get => (double)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    public double Radius { get => (double)GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }
    public string Glyph { get => (string)GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }
    public bool Stretch { get => (bool)GetValue(StretchProperty); set => SetValue(StretchProperty, value); }

    public static ImageUrlBuilder? UrlBuilder { get; set; }
public static string? PublicBaseUrl { get; set; }

    private readonly Image _image = new() { Aspect = Aspect.AspectFill };
    private readonly Label _placeholder = new()
    {
        FontFamily = "MDI",
        FontSize = 22,
        HorizontalOptions = LayoutOptions.Center,
        VerticalOptions = LayoutOptions.Center,
    };
    private readonly Border _border;

    public Thumb()
    {
        _border = new Border
        {
            StrokeThickness = 0,
            Content = new Grid { Children = { _placeholder, _image } },
        };
        Content = _border;
        Apply();
    }

    private static void OnChanged(BindableObject bindable, object oldValue, object newValue) => ((Thumb)bindable).Apply();

    private void Apply()
    {
        _border.WidthRequest = Stretch ? -1 : Size;
        _border.HeightRequest = Size;
        _border.HorizontalOptions = Stretch ? LayoutOptions.Fill : LayoutOptions.Center;
        _border.StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(Radius) };
        _border.SetAppTheme(BackgroundColorProperty, Color.FromArgb("#F1F3F5"), Color.FromArgb("#1A1F1C"));
        _placeholder.Text = Glyph;
        _placeholder.FontSize = Math.Clamp(Size / 2.8, 16, 48);
        _placeholder.SetAppTheme(Label.TextColorProperty, Color.FromArgb("#9CA3AF"), Color.FromArgb("#6B7280"));

        // Resolve image source
        string? resolvedUrl = null;
        if (!string.IsNullOrWhiteSpace(Source))
        {
            if (Uri.TryCreate(Source, UriKind.Absolute, out var absolute) &&
                (absolute.Scheme == "http" || absolute.Scheme == "https"))
            {
                resolvedUrl = Source;
            }
            else if (UrlBuilder != null)
            {
                resolvedUrl = UrlBuilder.Full(Source, thumb: true);
            }
            else if (!string.IsNullOrWhiteSpace(PublicBaseUrl))
            {
                var escaped = Uri.EscapeDataString(Source);
                resolvedUrl = $"{PublicBaseUrl.TrimEnd('/')}/api/storage/content?key={escaped}";
            }
        }
        var valid = Uri.TryCreate(resolvedUrl, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == "http" || uri.Scheme == "https");
        _image.Source = valid ? ImageSource.FromUri(uri) : null;
        _image.IsVisible = valid;
    }
}
