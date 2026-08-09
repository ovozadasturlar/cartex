namespace Cartex.Mobile.Core.Controls;

public sealed class Thumb : ContentView
{
    private static readonly HttpClient ImageClient = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

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
    public static Func<CancellationToken, Task<string?>>? AccessTokenProvider { get; set; }

    private readonly Image _image = new() { Aspect = Aspect.AspectFill };
    private readonly Label _placeholder = new()
    {
        FontFamily = "MDI",
        FontSize = 22,
        HorizontalOptions = LayoutOptions.Center,
        VerticalOptions = LayoutOptions.Center,
    };
    private readonly Border _border;
    private long _imageRequestVersion;

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

        var requestVersion = Interlocked.Increment(ref _imageRequestVersion);
        _image.Source = null;
        _image.IsVisible = false;
        _placeholder.IsVisible = true;

        // Resolve image source.
        string? resolvedUrl = null;
        var source = Source;
        if (!string.IsNullOrWhiteSpace(source))
        {
            if (Uri.TryCreate(source, UriKind.Absolute, out var absolute) &&
                (absolute.Scheme == "http" || absolute.Scheme == "https"))
            {
                resolvedUrl = source;
            }
            else if (UrlBuilder != null)
            {
                resolvedUrl = UrlBuilder.Full(source, thumb: true);
            }
            else if (!string.IsNullOrWhiteSpace(PublicBaseUrl))
            {
                var escaped = Uri.EscapeDataString(source);
                resolvedUrl = $"{PublicBaseUrl.TrimEnd('/')}/api/storage/content?key={escaped}";
            }
        }
        var valid = Uri.TryCreate(resolvedUrl, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == "http" || uri.Scheme == "https");
        if (valid)
            _ = LoadImageAsync(uri!, requestVersion);
    }

    private async Task LoadImageAsync(Uri uri, long requestVersion)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            var token = AccessTokenProvider is null
                ? null
                : await AccessTokenProvider(CancellationToken.None);
            if (!string.IsNullOrWhiteSpace(token))
                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            System.Diagnostics.Debug.WriteLine($"CartexThumb GET {uri} (token: {!string.IsNullOrWhiteSpace(token)})");
            using var response = await ImageClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            var bytes = await response.Content.ReadAsByteArrayAsync();
            System.Diagnostics.Debug.WriteLine($"CartexThumb OK {(int)response.StatusCode}, {bytes.Length} bytes: {uri}");
            if (requestVersion != Interlocked.Read(ref _imageRequestVersion))
                return;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (requestVersion != Interlocked.Read(ref _imageRequestVersion))
                    return;

                // Loading through the app's HTTP client is reliable on Android for
                // remote HTTPS images; the platform UriImageSource was silently
                // failing and leaving the placeholder visible.
                _image.Source = ImageSource.FromStream(() => new MemoryStream(bytes, writable: false));
                _image.IsVisible = true;
                _placeholder.IsVisible = false;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CartexThumb FAILED {uri}: {ex}");
            ShowPlaceholder(requestVersion);
        }
    }

    private void ShowPlaceholder(long requestVersion)
    {
        if (requestVersion != Interlocked.Read(ref _imageRequestVersion))
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (requestVersion != Interlocked.Read(ref _imageRequestVersion))
                return;

            _image.Source = null;
            _image.IsVisible = false;
            _placeholder.IsVisible = true;
        });
    }
}
