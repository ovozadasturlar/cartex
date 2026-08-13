using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Cartex.UI.Controls;

public static class OverlayBehaviors
{
    public static readonly AttachedProperty<ICommand?> DismissCommandProperty =
        AvaloniaProperty.RegisterAttached<Control, ICommand?>("DismissCommand", typeof(OverlayBehaviors));

    public static ICommand? GetDismissCommand(Control c) => c.GetValue(DismissCommandProperty);
    public static void SetDismissCommand(Control c, ICommand? value) => c.SetValue(DismissCommandProperty, value);

    static OverlayBehaviors()
    {
        DismissCommandProperty.Changed.AddClassHandler<Control>((host, e) =>
        {
            host.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
            host.RemoveHandler(InputElement.KeyDownEvent, OnEscape);
            if (e.NewValue is ICommand)
            {
                host.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Bubble);
                host.AddHandler(InputElement.KeyDownEvent, OnEscape, RoutingStrategies.Bubble);
            }
        });
    }

    /// Every overlay already declares how it closes, so Escape reuses that command
    /// instead of each view wiring the key itself.
    private static void OnEscape(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || sender is not Control host || !host.IsEffectivelyVisible) return;
        if (GetDismissCommand(host) is not { } command || !command.CanExecute(null)) return;
        command.Execute(null);
        e.Handled = true;
    }

    private static void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control host || !ReferenceEquals(e.Source, host)) return;
        if (!e.GetCurrentPoint(host).Properties.IsLeftButtonPressed) return;
        if (GetDismissCommand(host) is not { } command || !command.CanExecute(null)) return;
        command.Execute(null);
        e.Handled = true;
    }
}
