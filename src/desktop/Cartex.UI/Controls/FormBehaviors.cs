using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Cartex.UI.Controls;

public static class FormBehaviors
{
    public static readonly AttachedProperty<bool> AutoFocusProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("AutoFocus", typeof(FormBehaviors));

    public static readonly AttachedProperty<ICommand?> EnterSubmitsProperty =
        AvaloniaProperty.RegisterAttached<Control, ICommand?>("EnterSubmits", typeof(FormBehaviors));

    public static readonly AttachedProperty<ICommand?> EscCancelsProperty =
        AvaloniaProperty.RegisterAttached<Control, ICommand?>("EscCancels", typeof(FormBehaviors));

    public static readonly AttachedProperty<bool> EnterMovesNextProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("EnterMovesNext", typeof(FormBehaviors));

    public static bool GetAutoFocus(Control c) => c.GetValue(AutoFocusProperty);
    public static void SetAutoFocus(Control c, bool value) => c.SetValue(AutoFocusProperty, value);
    public static ICommand? GetEnterSubmits(Control c) => c.GetValue(EnterSubmitsProperty);
    public static void SetEnterSubmits(Control c, ICommand? value) => c.SetValue(EnterSubmitsProperty, value);
    public static ICommand? GetEscCancels(Control c) => c.GetValue(EscCancelsProperty);
    public static void SetEscCancels(Control c, ICommand? value) => c.SetValue(EscCancelsProperty, value);
    public static bool GetEnterMovesNext(Control c) => c.GetValue(EnterMovesNextProperty);
    public static void SetEnterMovesNext(Control c, bool value) => c.SetValue(EnterMovesNextProperty, value);

    static FormBehaviors()
    {
        AutoFocusProperty.Changed.AddClassHandler<Control>((host, e) =>
        {
            host.PropertyChanged -= OnHostPropertyChanged;
            host.AttachedToVisualTree -= OnHostAttached;
            if (e.NewValue is true)
            {
                host.PropertyChanged += OnHostPropertyChanged;
                host.AttachedToVisualTree += OnHostAttached;
            }
        });

        EscCancelsProperty.Changed.AddClassHandler<Control>((host, e) =>
        {
            host.RemoveHandler(InputElement.KeyDownEvent, OnEscKeyDown);
            if (e.NewValue is ICommand)
                host.AddHandler(InputElement.KeyDownEvent, OnEscKeyDown, RoutingStrategies.Bubble);
        });

        EnterMovesNextProperty.Changed.AddClassHandler<Control>((input, e) =>
        {
            input.RemoveHandler(InputElement.KeyDownEvent, OnEnterKeyDown);
            if (e.NewValue is true)
                input.AddHandler(InputElement.KeyDownEvent, OnEnterKeyDown, RoutingStrategies.Bubble, handledEventsToo: true);
        });
    }

    private static void OnHostAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is Control { IsVisible: true } host)
            Dispatcher.UIThread.Post(() => FocusFirst(host), DispatcherPriority.Background);
    }

    private static void OnHostPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Visual.IsVisibleProperty && e.NewValue is true && sender is Control host)
            Dispatcher.UIThread.Post(() => FocusFirst(host), DispatcherPriority.Background);
    }

    private static void FocusFirst(Control host)
    {
        if (!host.IsVisible || !host.IsAttachedToVisualTree()) return;
        var first = FormInputs(host).FirstOrDefault();
        if (first is not null) FocusInput(first);
    }

    private static void OnEscKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || sender is not Control host) return;
        if (GetEscCancels(host) is not { } command || !command.CanExecute(null)) return;
        command.Execute(null);
        e.Handled = true;
    }

    private static void OnEnterKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Return) || sender is not Control input) return;
        if (e.Source is not Control source || !ReferenceEquals(FindFlagged(source), input)) return;
        if (input is TextBox { AcceptsReturn: true }) return;
        if (input.FindAncestorOfType<ComboBox>(true) is { IsDropDownOpen: true }) return;
        if (input.FindAncestorOfType<AutoCompleteBox>(true) is { IsDropDownOpen: true }) return;

        var current = Normalize(input);
        var scope = current.GetVisualAncestors().OfType<Control>().FirstOrDefault(a => a.Classes.Contains("form"))
            ?? TopLevel.GetTopLevel(current) as Control;
        if (scope is null) return;

        var inputs = FormInputs(scope).ToList();
        var index = inputs.IndexOf(current);
        if (index < 0) return;

        if (index + 1 < inputs.Count)
        {
            FocusInput(inputs[index + 1]);
            e.Handled = true;
            return;
        }

        var submitHost = current.GetVisualAncestors().OfType<Control>()
            .FirstOrDefault(a => GetEnterSubmits(a) is not null);
        if (submitHost is not null && GetEnterSubmits(submitHost) is { } submit && submit.CanExecute(null))
        {
            submit.Execute(null);
            e.Handled = true;
        }
    }

    private static Control? FindFlagged(Control source) =>
        source.GetSelfAndVisualAncestors().OfType<Control>().FirstOrDefault(GetEnterMovesNext);

    private static Control Normalize(Control input) =>
        input.FindAncestorOfType<NumericUpDown>(true) as Control
        ?? input.FindAncestorOfType<AutoCompleteBox>(true) as Control
        ?? input.FindAncestorOfType<CalendarDatePicker>(true) as Control
        ?? input.FindAncestorOfType<ComboBox>(true) as Control
        ?? input;

    private static IEnumerable<Control> FormInputs(Control scope) =>
        scope.GetVisualDescendants()
            .OfType<Control>()
            .Where(c => c is NumericUpDown or AutoCompleteBox or ComboBox or CalendarDatePicker
                || (c is TextBox && Normalize(c) is TextBox))
            .Where(c => c.IsEffectivelyVisible && c.IsEffectivelyEnabled);

    private static void FocusInput(Control input)
    {
        var target = input switch
        {
            NumericUpDown or AutoCompleteBox or CalendarDatePicker =>
                input.GetVisualDescendants().OfType<TextBox>().FirstOrDefault() as Control ?? input,
            _ => input
        };
        target.Focus();
        if (target is TextBox box) box.SelectAll();
    }
}
