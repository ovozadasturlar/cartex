using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Cartex.UI.Controls;

public sealed class FocusNavigator
{
    private readonly List<Control> _ring;

    private FocusNavigator(IReadOnlyList<Control> ring) => _ring = [.. ring];

    public static FocusNavigator Attach(Control owner, params Control[] ring)
    {
        var navigator = new FocusNavigator(ring);

        foreach (var element in ring)
            element.AddHandler(InputElement.KeyDownEvent, navigator.OnKeyDown, RoutingStrategies.Tunnel);

        owner.DetachedFromVisualTree += (_, _) =>
        {
            foreach (var element in ring)
                element.RemoveHandler(InputElement.KeyDownEvent, navigator.OnKeyDown);
        };

        return navigator;
    }

    public void FocusFirst() => Focus(_ring.FirstOrDefault(Focusable));

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not Control element) return;

        var index = _ring.IndexOf(element);
        if (index < 0) return;

        var back = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var target = e.Key switch
        {
            Key.Enter or Key.Return when Bound(element) => null,
            Key.Enter or Key.Return or Key.Tab when element is Button button => Press(button, e, back, index),
            Key.Enter or Key.Return or Key.Tab => Next(index, !back),
            Key.Left or Key.Right or Key.Up or Key.Down when Dropped(element) => null,
            Key.Left or Key.Right or Key.Up or Key.Down when element is ListBox => null,
            Key.Up or Key.Down when element is ComboBox => null,
            Key.Right when Inner(element) is { } right => Caret(right, atEnd: true) ? Next(index, !back) : null,
            Key.Left when Inner(element) is { } left => Caret(left, atEnd: false) ? Next(index, back) : null,
            Key.Right or Key.Down => Next(index, !back),
            Key.Left or Key.Up => Next(index, back),
            _ => null
        };

        if (target is null) return;
        Focus(target);
        e.Handled = true;
    }

    private Control? Press(Button button, KeyEventArgs e, bool back, int index)
    {
        if (e.Key is Key.Tab) return Next(index, !back);
        if (back) return Next(index, false);

        if (button.Command?.CanExecute(button.CommandParameter) == true)
            button.Command.Execute(button.CommandParameter);
        e.Handled = true;
        return null;
    }

    private Control? Next(int index, bool forward)
    {
        var step = forward ? 1 : -1;
        for (var i = 1; i <= _ring.Count; i++)
        {
            var next = (index + i * step + _ring.Count) % _ring.Count;
            if (next != index && Focusable(_ring[next])) return _ring[next];
        }
        return null;
    }

    private static bool Focusable(Control control) => control.IsEffectivelyVisible && control.IsEffectivelyEnabled;

    private static bool Bound(Control control) =>
        control.KeyBindings.Any(b => b.Gesture.Key is Key.Enter or Key.Return && b.Gesture.KeyModifiers == KeyModifiers.None);

    private static bool Dropped(Control control) =>
        control.GetSelfAndVisualDescendants().OfType<ComboBox>().Any(c => c.IsDropDownOpen)
        || control.GetSelfAndVisualDescendants().OfType<AutoCompleteBox>().Any(c => c.IsDropDownOpen);

    private static TextBox? Inner(Control control) =>
        control as TextBox ?? control.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(t => t.IsFocused);

    private static bool Caret(TextBox box, bool atEnd)
    {
        var text = box.Text ?? string.Empty;
        if (box.SelectionStart != box.SelectionEnd && Math.Abs(box.SelectionEnd - box.SelectionStart) == text.Length)
            return true;
        return atEnd ? box.CaretIndex >= text.Length : box.CaretIndex == 0;
    }

    private static void Focus(Control? control)
    {
        if (control is null) return;

        Dispatcher.UIThread.Post(() =>
        {
            control.Focus();
            if (control.GetSelfAndVisualDescendants().OfType<TextBox>().FirstOrDefault() is not { } box) return;
            box.Focus();
            box.SelectAll();
        }, DispatcherPriority.Input);
    }
}
