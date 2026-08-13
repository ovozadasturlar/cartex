using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Cartex.UI.Controls;

/// A till rests its focus in the scan box. Committing an amount with Enter, or clicking
/// anywhere that is not an input, hands the keyboard straight back to the scanner.
public static class ScanFocus
{
    public static readonly AttachedProperty<Control?> TargetProperty =
        AvaloniaProperty.RegisterAttached<Control, Control?>("Target", typeof(ScanFocus));

    public static Control? GetTarget(Control host) => host.GetValue(TargetProperty);
    public static void SetTarget(Control host, Control? value) => host.SetValue(TargetProperty, value);

    static ScanFocus()
    {
        TargetProperty.Changed.AddClassHandler<Control>((host, e) =>
        {
            host.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
            host.RemoveHandler(InputElement.PointerReleasedEvent, OnPointerReleased);
            if (e.NewValue is Control)
            {
                host.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble, handledEventsToo: true);
                host.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Bubble, handledEventsToo: true);
            }
        });
    }

    private static void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is (Key.Enter or Key.Return) && sender is Control host)
            Restore(host);
    }

    private static void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is not Control host || e.Source is not Control source) return;
        if (source.GetSelfAndVisualAncestors().OfType<Control>().Any(IsInteractive)) return;
        Restore(host);
    }

    private static bool IsInteractive(Control control) =>
        control is TextBox or NumericUpDown or AutoCompleteBox or ComboBox or CalendarDatePicker
            or Button or ToggleButton or MenuItem;

    private static void Restore(Control host)
    {
        if (GetTarget(host) is not { } target) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (!target.IsEffectivelyVisible || !target.IsEffectivelyEnabled) return;
            target.Focus();
            if (target is TextBox box) box.SelectAll();
        }, DispatcherPriority.Input);
    }
}
