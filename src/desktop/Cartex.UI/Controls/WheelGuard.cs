using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Cartex.UI.Controls;

/// Scrolling a list while the cursor passes over a focused amount or picker used to
/// overwrite it. The wheel now never edits a value - it scrolls the surrounding list.
public static class WheelGuard
{
    private const int LinesPerNotch = 3;

    public static void Install()
    {
        InputElement.PointerWheelChangedEvent.AddClassHandler<NumericUpDown>(Scroll, RoutingStrategies.Tunnel);
        InputElement.PointerWheelChangedEvent.AddClassHandler<ComboBox>(Scroll, RoutingStrategies.Tunnel);
    }

    private static void Scroll(Control source, PointerWheelEventArgs e)
    {
        if (source is ComboBox { IsDropDownOpen: true }) return;

        e.Handled = true;
        if (source.FindAncestorOfType<ScrollViewer>() is not { } scroll) return;

        for (var i = 0; i < LinesPerNotch; i++)
        {
            if (e.Delta.Y > 0) scroll.LineUp();
            else if (e.Delta.Y < 0) scroll.LineDown();
        }
    }
}
