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
            if (e.NewValue is ICommand)
                host.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Bubble);
        });
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
