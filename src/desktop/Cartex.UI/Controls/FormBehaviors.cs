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

    /// Ctrl+Enter saves from anywhere in the form, so plain Enter stays free for field
    /// navigation and for multi-line notes.
    public static readonly AttachedProperty<ICommand?> SaveShortcutProperty =
        AvaloniaProperty.RegisterAttached<Control, ICommand?>("SaveShortcut", typeof(FormBehaviors));

    /// Ctrl+Shift+Enter saves and immediately starts another entry.
    public static readonly AttachedProperty<ICommand?> SaveAndNewShortcutProperty =
        AvaloniaProperty.RegisterAttached<Control, ICommand?>("SaveAndNewShortcut", typeof(FormBehaviors));

    public static readonly AttachedProperty<bool> EnterMovesNextProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("EnterMovesNext", typeof(FormBehaviors));

    public static readonly AttachedProperty<bool> CloseOnSelectProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("CloseOnSelect", typeof(FormBehaviors));

    /// Tahrirlanadigan tanlov maydoni: fokusga kelganda ro'yxat ochiladi, matn esa
    /// erkin yoziladi — ComboBox tanlash va TextBox yozish birlashadi.
    public static readonly AttachedProperty<bool> OpenOnFocusProperty =
        AvaloniaProperty.RegisterAttached<AutoCompleteBox, bool>("OpenOnFocus", typeof(FormBehaviors));

    public static readonly AttachedProperty<bool> SelectAllOnFocusProperty =
        AvaloniaProperty.RegisterAttached<TextBox, bool>("SelectAllOnFocus", typeof(FormBehaviors));

    /// Counts are whole numbers, so letters and separators never reach the field.
    public static readonly AttachedProperty<bool> DigitsOnlyProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("DigitsOnly", typeof(FormBehaviors));

    public static readonly AttachedProperty<ICommand?> LostFocusCommandProperty =
        AvaloniaProperty.RegisterAttached<Control, ICommand?>("LostFocusCommand", typeof(FormBehaviors));

    public static bool GetAutoFocus(Control c) => c.GetValue(AutoFocusProperty);
    public static void SetAutoFocus(Control c, bool value) => c.SetValue(AutoFocusProperty, value);
    public static ICommand? GetEnterSubmits(Control c) => c.GetValue(EnterSubmitsProperty);
    public static void SetEnterSubmits(Control c, ICommand? value) => c.SetValue(EnterSubmitsProperty, value);
    public static ICommand? GetEscCancels(Control c) => c.GetValue(EscCancelsProperty);
    public static void SetEscCancels(Control c, ICommand? value) => c.SetValue(EscCancelsProperty, value);
    public static ICommand? GetSaveShortcut(Control c) => c.GetValue(SaveShortcutProperty);
    public static void SetSaveShortcut(Control c, ICommand? value) => c.SetValue(SaveShortcutProperty, value);
    public static ICommand? GetSaveAndNewShortcut(Control c) => c.GetValue(SaveAndNewShortcutProperty);
    public static void SetSaveAndNewShortcut(Control c, ICommand? value) => c.SetValue(SaveAndNewShortcutProperty, value);
    public static bool GetEnterMovesNext(Control c) => c.GetValue(EnterMovesNextProperty);
    public static void SetEnterMovesNext(Control c, bool value) => c.SetValue(EnterMovesNextProperty, value);
    public static bool GetCloseOnSelect(Control c) => c.GetValue(CloseOnSelectProperty);
    public static void SetCloseOnSelect(Control c, bool value) => c.SetValue(CloseOnSelectProperty, value);
    public static bool GetOpenOnFocus(AutoCompleteBox c) => c.GetValue(OpenOnFocusProperty);
    public static void SetOpenOnFocus(AutoCompleteBox c, bool value) => c.SetValue(OpenOnFocusProperty, value);
    public static bool GetSelectAllOnFocus(TextBox c) => c.GetValue(SelectAllOnFocusProperty);
    public static void SetSelectAllOnFocus(TextBox c, bool value) => c.SetValue(SelectAllOnFocusProperty, value);
    public static bool GetDigitsOnly(Control c) => c.GetValue(DigitsOnlyProperty);
    public static void SetDigitsOnly(Control c, bool value) => c.SetValue(DigitsOnlyProperty, value);
    public static ICommand? GetLostFocusCommand(Control c) => c.GetValue(LostFocusCommandProperty);
    public static void SetLostFocusCommand(Control c, ICommand? value) => c.SetValue(LostFocusCommandProperty, value);

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

        EnterSubmitsProperty.Changed.AddClassHandler<Control>((host, e) =>
        {
            host.RemoveHandler(InputElement.KeyDownEvent, OnEnterSubmit);
            if (e.NewValue is ICommand)
                host.AddHandler(InputElement.KeyDownEvent, OnEnterSubmit, RoutingStrategies.Bubble);
        });

        SaveShortcutProperty.Changed.AddClassHandler<Control>((host, e) =>
        {
            host.RemoveHandler(InputElement.KeyDownEvent, OnSaveShortcut);
            if (e.NewValue is ICommand)
                host.AddHandler(InputElement.KeyDownEvent, OnSaveShortcut, RoutingStrategies.Bubble, handledEventsToo: true);
        });

        SaveAndNewShortcutProperty.Changed.AddClassHandler<Control>((host, e) =>
        {
            host.RemoveHandler(InputElement.KeyDownEvent, OnSaveShortcut);
            if (e.NewValue is ICommand)
                host.AddHandler(InputElement.KeyDownEvent, OnSaveShortcut, RoutingStrategies.Bubble, handledEventsToo: true);
        });

        EnterMovesNextProperty.Changed.AddClassHandler<Control>((input, e) =>
        {
            input.RemoveHandler(InputElement.KeyDownEvent, OnEnterKeyDown);
            if (e.NewValue is true)
                input.AddHandler(InputElement.KeyDownEvent, OnEnterKeyDown, RoutingStrategies.Bubble, handledEventsToo: true);
        });

        DigitsOnlyProperty.Changed.AddClassHandler<Control>((host, e) =>
        {
            host.RemoveHandler(InputElement.TextInputEvent, OnDigitsOnlyInput);
            if (e.NewValue is true)
                host.AddHandler(InputElement.TextInputEvent, OnDigitsOnlyInput, RoutingStrategies.Tunnel);
        });

        SelectAllOnFocusProperty.Changed.AddClassHandler<TextBox>((box, e) =>
        {
            box.GotFocus -= OnSelectAllFocus;
            if (e.NewValue is true)
                box.GotFocus += OnSelectAllFocus;
        });

        CloseOnSelectProperty.Changed.AddClassHandler<AutoCompleteBox>((box, e) =>
        {
            box.SelectionChanged -= OnSelectClose;
            box.RemoveHandler(InputElement.KeyUpEvent, OnCommitKeyUp);
            if (e.NewValue is true)
            {
                box.SelectionChanged += OnSelectClose;
                box.AddHandler(InputElement.KeyUpEvent, OnCommitKeyUp, RoutingStrategies.Tunnel, handledEventsToo: true);
            }
        });

        OpenOnFocusProperty.Changed.AddClassHandler<AutoCompleteBox>((box, e) =>
        {
            box.GotFocus -= OnOpenFocus;
            if (e.NewValue is true)
                box.GotFocus += OnOpenFocus;
        });

        LostFocusCommandProperty.Changed.AddClassHandler<Control>((control, e) =>
        {
            control.LostFocus -= OnLostFocus;
            if (e.NewValue is ICommand)
                control.LostFocus += OnLostFocus;
        });
    }

    private static void OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control || GetLostFocusCommand(control) is not { } command || !command.CanExecute(null)) return;
        command.Execute(null);
    }

    private static void OnOpenFocus(object? sender, FocusChangedEventArgs e)
    {
        if (sender is not AutoCompleteBox box) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (box.IsKeyboardFocusWithin && !box.IsDropDownOpen)
                box.IsDropDownOpen = true;
        }, DispatcherPriority.Background);
    }

    private static void OnDigitsOnlyInput(object? sender, TextInputEventArgs e)
    {
        if (e.Text is { Length: > 0 } text && !text.All(char.IsAsciiDigit))
            e.Handled = true;
    }

    private static void OnSelectAllFocus(object? sender, FocusChangedEventArgs e)
    {
        if (sender is TextBox box)
            Dispatcher.UIThread.Post(box.SelectAll, DispatcherPriority.Background);
    }

    private static void OnSelectClose(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && sender is AutoCompleteBox box)
            CloseDropDown(box);
    }

    private static void OnCommitKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key is (Key.Enter or Key.Return) && sender is AutoCompleteBox box)
            CloseDropDown(box);
    }

    private static void CloseDropDown(AutoCompleteBox box) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (box.IsDropDownOpen) box.IsDropDownOpen = false;
        }, DispatcherPriority.Background);

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

    /// Oyna ochilganda kursor birinchi maydonga tushadi: kassir sichqonchani qidirmasin.
    public static void FocusFirst(Control host)
    {
        if (!host.IsVisible || !host.IsAttachedToVisualTree()) return;
        var first = FormInputs(host).FirstOrDefault();
        if (first is not null) FocusInput(first);
    }

    /// Formadagi maydon Enter'ni o'zi ishlab, fokusni keyingisiga surgan bo'lsa hodisa
    /// allaqachon yopilgan bo'ladi; bu yerga faqat forma tugagan yoki forma umuman
    /// bo'lmagan holat (oddiy dialog) yetib keladi.
    private static void OnEnterSubmit(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.Key is not (Key.Enter or Key.Return) || sender is not Control host) return;
        if (e.KeyModifiers != KeyModifiers.None) return;
        if (e.Source is TextBox { AcceptsReturn: true }) return;
        if (e.Source is Control source
            && (source.FindAncestorOfType<ComboBox>(true) is { IsDropDownOpen: true }
                || source.FindAncestorOfType<AutoCompleteBox>(true) is { IsDropDownOpen: true })) return;
        if (GetEnterSubmits(host) is not { } command || !command.CanExecute(null)) return;
        command.Execute(null);
        e.Handled = true;
    }

    private static void OnEscKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || sender is not Control host) return;
        if (GetEscCancels(host) is not { } command || !command.CanExecute(null)) return;
        command.Execute(null);
        e.Handled = true;
    }

    private static void OnSaveShortcut(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Return) || sender is not Control host) return;
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;

        var command = e.KeyModifiers.HasFlag(KeyModifiers.Shift)
            ? GetSaveAndNewShortcut(host) ?? GetSaveShortcut(host)
            : GetSaveShortcut(host);
        if (command is null || !command.CanExecute(null)) return;

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

        var current = Normalize(e.Source as Control ?? input);
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
        ?? input.FindAncestorOfType<DatePicker>(true) as Control
        ?? input.FindAncestorOfType<ComboBox>(true) as Control
        ?? input;

    private static IEnumerable<Control> FormInputs(Control scope) =>
        scope.GetVisualDescendants()
            .OfType<Control>()
            .Where(c => (c is NumericUpDown or AutoCompleteBox or ComboBox or CalendarDatePicker or DatePicker
                         && ReferenceEquals(Normalize(c), c))
                || (c is TextBox && Normalize(c) is TextBox))
            .Where(c => c.IsEffectivelyVisible && c.IsEffectivelyEnabled);

    private static void FocusInput(Control input)
    {
        var target = input switch
        {
            NumericUpDown or AutoCompleteBox or CalendarDatePicker or DatePicker =>
                input.GetVisualDescendants().OfType<TextBox>().FirstOrDefault() as Control ?? input,
            _ => input
        };
        target.Focus();
        if (target is TextBox box) box.SelectAll();
    }
}
