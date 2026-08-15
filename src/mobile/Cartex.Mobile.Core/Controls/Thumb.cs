using System.Security.Cryptography;
using System.Text;

namespace Cartex.Mobile.Core.Controls;

public sealed class Thumb : ContentView
{
    private static readonly HttpClient ImageClient = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    private static string? _cacheDir;

    public static readonly BindableProperty SourceProperty =
        BindableProperty.Create(nameof(Source), typeof(string), typeof(Thumb),
            propertyChanged: (b, _, _) => ((Thumb)b).ApplySource());

    public static readonly BindableProperty SizeProperty =
        BindableProperty.Create(nameof(Size), typeof(double), typeof(Thumb), 64d,
            propertyChanged: (b, _, _) => ((Thumb)b).ApplyLayout());

    public static readonly BindableProperty RadiusProperty =
        BindableProperty.Create(nameof(Radius), typeof(double), typeof(Thumb), 12d,
            propertyChanged: (b, _, _) => ((Thumb)b).ApplyShape());

    public static readonly BindableProperty GlyphProperty =
        BindableProperty.Create(nameof(Glyph), typeof(string), typeof(Thumb), "\U000f03d7",
            propertyChanged: (b, _, _) => ((Thumb)b).ApplyGlyph());

    public static readonly BindableProperty StretchProperty =
        BindableProperty.Create(nameof(Stretch), typeof(bool), typeof(Thumb), false,
            propertyChanged: (b, _, _) => ((Thumb)b).ApplyLayout());

    public string? Source { get => (string?)GetValue(SourceProperty); set => SetValue(SourceProperty, value); }
    public double Size { get => (double)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    public double Radius { get => (double)GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }
    public string Glyph { get => (string)GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }
    public bool Stretch { get => (bool)GetValue(StretchProperty); set => SetValue(StretchProperty, value); }

    public static ImageUrlBuilder? UrlBuilder { get; set; }

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
        _border.SetAppTheme(BackgroundColorProperty, Color.FromArgb("#F1F3F5"), Color.FromArgb("#1A1F1C"));
        _placeholder.SetAppTheme(Label.TextColorProperty, Color.FromArgb("#9CA3AF"), Color.FromArgb("#6B7280"));
        Content = _border;
        ApplyLayout();
        ApplyShape();
        ApplyGlyph();
    }

    private void ApplyLayout()
    {
        _border.WidthRequest = Stretch ? -1 : Size;
        _border.HeightRequest = Size;
        _border.HorizontalOptions = Stretch ? LayoutOptions.Fill : LayoutOptions.Center;
        _placeholder.FontSize = Math.Clamp(Size / 2.8, 16, 48);
    }

    private void ApplyShape() =>
        _border.StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(Radius) };

    private void ApplyGlyph() => _placeholder.Text = Glyph;

    private void ApplySource()
    {
        var requestVersion = Interlocked.Increment(ref _imageRequestVersion);
        _image.Source = null;
        _image.IsVisible = false;
        _placeholder.IsVisible = true;

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
                resolvedUrl = source.StartsWith('/')
                    ? UrlBuilder.Full(source, thumb: true)
                    : UrlBuilder.FromKey(source, thumb: true);
            }
        }
        var valid = Uri.TryCreate(resolvedUrl, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == "http" || uri.Scheme == "https");
        if (!valid)
            return;

        var cachePath = CachePathFor(resolvedUrl!);
        if (File.Exists(cachePath))
        {
            ShowImage(cachePath);
            return;
        }
        _ = Task.Run(() => LoadImageAsync(uri!, cachePath, requestVersion));
    }

    private async Task LoadImageAsync(Uri uri, string cachePath, long requestVersion)
    {
        try
        {
            using var response = await ImageClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            var tmp = $"{cachePath}.{Guid.NewGuid():N}.tmp";
            await using (var file = File.Create(tmp))
                await response.Content.CopyToAsync(file);
            File.Move(tmp, cachePath, overwrite: true);
        }
        catch
        {
            ShowPlaceholder(requestVersion);
            return;
        }

        if (requestVersion != Interlocked.Read(ref _imageRequestVersion))
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (requestVersion != Interlocked.Read(ref _imageRequestVersion))
                return;
            ShowImage(cachePath);
        });
    }

    private void ShowImage(string cachePath)
    {
        _image.Source = ImageSource.FromFile(cachePath);
        _image.IsVisible = true;
        _placeholder.IsVisible = false;
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

    private static string CachePathFor(string url)
    {
        var dir = _cacheDir ??= EnsureCacheDir();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)));
        return Path.Combine(dir, hash);
    }

    private static string EnsureCacheDir()
    {
        var dir = Path.Combine(FileSystem.CacheDirectory, "thumbs");
        Directory.CreateDirectory(dir);
        _ = Task.Run(() => PurgeOld(dir));
        return dir;
    }

    private static void PurgeOld(string dir)
    {
        var cutoff = DateTime.UtcNow.AddDays(-14);
        try
        {
            foreach (var file in Directory.EnumerateFiles(dir))
                if (File.GetLastWriteTimeUtc(file) < cutoff)
                    File.Delete(file);
        }
        catch
        {
        }
    }
}
