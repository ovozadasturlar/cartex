using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Cartex.UI.Controls;

public static class PressGestures
{
    private const double MoveTolerance = 8;
    private static readonly TimeSpan HoldDelay = TimeSpan.FromMilliseconds(550);

    public static readonly AttachedProperty<ICommand?> CommandProperty =
        AvaloniaProperty.RegisterAttached<Control, ICommand?>("Command", typeof(PressGestures));

    public static ICommand? GetCommand(Control c) => c.GetValue(CommandProperty);
    public static void SetCommand(Control c, ICommand? value) => c.SetValue(CommandProperty, value);

    private static readonly AttachedProperty<Press?> StateProperty =
        AvaloniaProperty.RegisterAttached<Control, Press?>("State", typeof(PressGestures));

    static PressGestures()
    {
        CommandProperty.Changed.AddClassHandler<Control>((host, e) =>
        {
            Cancel(host);
            host.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
            host.RemoveHandler(InputElement.PointerReleasedEvent, OnPointerReleased);
            host.RemoveHandler(InputElement.PointerMovedEvent, OnPointerMoved);
            host.RemoveHandler(InputElement.PointerCaptureLostEvent, OnPointerCaptureLost);
            host.RemoveHandler(InputElement.PointerExitedEvent, OnPointerExited);
            host.RemoveHandler(InputElement.HoldingEvent, OnHolding);

            if (e.NewValue is not ICommand) return;

            host.AddHandler(InputElement.HoldingEvent, OnHolding, RoutingStrategies.Bubble, handledEventsToo: true);
            host.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Bubble, handledEventsToo: true);
            host.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Bubble, handledEventsToo: true);
            host.AddHandler(InputElement.PointerMovedEvent, OnPointerMoved, RoutingStrategies.Bubble, handledEventsToo: true);
            host.AddHandler(InputElement.PointerCaptureLostEvent, OnPointerCaptureLost, RoutingStrategies.Direct, handledEventsToo: true);
            host.AddHandler(InputElement.PointerExitedEvent, OnPointerExited, RoutingStrategies.Direct, handledEventsToo: true);
        });
    }

    private static void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control host) return;
        Cancel(host);

        if (e.Pointer.Type != PointerType.Mouse) return;
        if (GetCommand(host) is not { } command || Blocked(host, e)) return;

        var point = e.GetCurrentPoint(host);
        if (point.Properties.IsRightButtonPressed)
        {
            Execute(host, command);
            e.Handled = true;
            return;
        }

        if (!point.Properties.IsLeftButtonPressed) return;

        var state = new Press
        {
            Origin = point.Position,
            Pointer = e.Pointer,
            Timer = new DispatcherTimer { Interval = HoldDelay }
        };
        state.Timer.Tick += (_, _) => Fire(host);
        host.SetValue(StateProperty, state);
        state.Timer.Start();
    }

    private static void OnHolding(object? sender, HoldingRoutedEventArgs e)
    {
        if (sender is not Control host || e.HoldingState != HoldingState.Started) return;
        if (e.PointerType == PointerType.Mouse) return;
        if (GetCommand(host) is not { } command) return;

        Execute(host, command);
        e.Handled = true;
    }

    private static void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not Control host || host.GetValue(StateProperty) is not { } state) return;
        var delta = e.GetPosition(host) - state.Origin;
        if (Math.Abs(delta.X) > MoveTolerance || Math.Abs(delta.Y) > MoveTolerance) Cancel(host);
    }

    private static void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is Control host) Cancel(host);
    }

    private static void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (sender is Control host) Cancel(host);
    }

    private static void OnPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is Control host) Cancel(host);
    }

    private static void Fire(Control host)
    {
        var pointer = host.GetValue(StateProperty)?.Pointer;
        Cancel(host);
        if (GetCommand(host) is not { } command) return;
        pointer?.Capture(null);
        Execute(host, command);
    }

    private static void Cancel(Control host)
    {
        if (host.GetValue(StateProperty) is not { } state) return;
        host.SetValue(StateProperty, null);
        state.Timer.Stop();
    }

    private static void Execute(Control host, ICommand command)
    {
        var parameter = host.DataContext;
        if (command.CanExecute(parameter)) command.Execute(parameter);
    }

    private static bool Blocked(Control host, PointerPressedEventArgs e)
    {
        if (host is Button) return false;
        if (e.Source is not Visual source) return false;

        foreach (var visual in source.GetSelfAndVisualAncestors())
        {
            if (ReferenceEquals(visual, host)) return false;
            if (visual is Button or NumericUpDown or TextBox) return true;
        }
        return false;
    }

    private sealed class Press
    {
        public required Point Origin { get; init; }
        public required IPointer Pointer { get; init; }
        public required DispatcherTimer Timer { get; init; }
    }
}
