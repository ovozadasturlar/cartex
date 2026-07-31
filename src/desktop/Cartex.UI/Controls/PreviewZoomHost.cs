using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Cartex.UI.Controls;

public sealed class PreviewZoomHost : Decorator
{
    private const double MinZoom = 0.5;
    private const double MaxZoom = 2;
    private const double ZoomStep = 0.1;
    private readonly ScaleTransform _scale = new();

    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<PreviewZoomHost, double>(nameof(Zoom), 1);

    static PreviewZoomHost()
    {
        AffectsMeasure<PreviewZoomHost>(ZoomProperty);
        AffectsArrange<PreviewZoomHost>(ZoomProperty);
    }

    public PreviewZoomHost()
    {
        Focusable = true;
        RenderTransformOrigin = RelativePoint.TopLeft;
    }

    public double Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, Math.Clamp(value, MinZoom, MaxZoom));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is null)
            return default;

        var zoom = SafeZoom;
        Child.Measure(new Size(
            double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : availableSize.Width / zoom,
            double.IsInfinity(availableSize.Height) ? double.PositiveInfinity : availableSize.Height / zoom));
        return new Size(
            Child.DesiredSize.Width * zoom,
            Child.DesiredSize.Height * zoom);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child is null)
            return finalSize;

        var zoom = SafeZoom;
        _scale.ScaleX = zoom;
        _scale.ScaleY = zoom;
        Child.RenderTransformOrigin = RelativePoint.TopLeft;
        Child.RenderTransform = _scale;
        Child.Arrange(new Rect(
            0,
            0,
            finalSize.Width / zoom,
            finalSize.Height / zoom));
        return finalSize;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        Focus(NavigationMethod.Pointer);
        base.OnPointerPressed(e);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            ChangeZoom(e.Delta.Y > 0 ? ZoomStep : -ZoomStep);
            e.Handled = true;
            return;
        }

        base.OnPointerWheelChanged(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            base.OnKeyDown(e);
            return;
        }

        var delta = e.Key switch
        {
            Key.Add or Key.OemPlus => ZoomStep,
            Key.Subtract or Key.OemMinus => -ZoomStep,
            _ => 0
        };
        if (delta == 0)
        {
            base.OnKeyDown(e);
            return;
        }

        ChangeZoom(delta);
        e.Handled = true;
    }

    private double SafeZoom => Math.Clamp(Zoom, MinZoom, MaxZoom);

    private void ChangeZoom(double delta) =>
        Zoom = Math.Round(SafeZoom + delta, 1, MidpointRounding.AwayFromZero);
}
