using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Cartex.UI.Controls;

public static class MoneyInput
{
    public static readonly AttachedProperty<bool> GroupProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("Group", typeof(MoneyInput));

    public static bool GetGroup(Control c) => c.GetValue(GroupProperty);
    public static void SetGroup(Control c, bool value) => c.SetValue(GroupProperty, value);

    public static readonly AttachedProperty<bool> CleanProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("Clean", typeof(MoneyInput));

    public static bool GetClean(Control c) => c.GetValue(CleanProperty);
    public static void SetClean(Control c, bool value) => c.SetValue(CleanProperty, value);

    private static readonly AttachedProperty<bool> BusyProperty =
        AvaloniaProperty.RegisterAttached<TextBox, bool>("Busy", typeof(MoneyInput));

    private static readonly AttachedProperty<Control?> OwnerProperty =
        AvaloniaProperty.RegisterAttached<TextBox, Control?>("Owner", typeof(MoneyInput));

    static MoneyInput()
    {
        GroupProperty.Changed.AddClassHandler<Control>((control, _) => Toggle(control));
        CleanProperty.Changed.AddClassHandler<Control>((control, _) => Toggle(control));
    }

    private static void Toggle(Control control)
    {
        if (control is TextBox box)
        {
            Sync(box, control);
            return;
        }

        if (control is not TemplatedControl templated) return;

        templated.TemplateApplied -= OnTemplateApplied;
        templated.TemplateApplied += OnTemplateApplied;
        Attach(templated);
    }

    private static void OnTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        if (sender is Control control) Attach(control);
    }

    private static void Attach(Control control)
    {
        if (InnerBox(control) is { } box) Sync(box, control);
    }

    private static void Sync(TextBox box, Control owner)
    {
        box.SetValue(OwnerProperty, owner);
        box.TextChanged -= OnTextChanged;
        box.GotFocus -= OnGotFocus;
        box.RemoveHandler(InputElement.TextInputEvent, OnTextInput);
        box.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);

        var clean = GetClean(owner);
        if (clean)
        {
            box.GotFocus += OnGotFocus;
            box.AddHandler(InputElement.TextInputEvent, OnTextInput, RoutingStrategies.Tunnel);
            box.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
        }

        if (clean || GetGroup(owner)) box.TextChanged += OnTextChanged;
    }

    private static bool Applies(Control control) =>
        control is not NumericUpDown numeric
        || (numeric.FormatString is { Length: > 0 } format
            && (format.StartsWith('N') || format.StartsWith('n') || format.Contains("#,#")));

    private static TextBox? InnerBox(Control control) =>
        control as TextBox ?? control.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();

    private static void OnGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (sender is not TextBox box) return;

        box.SelectAll();
        var text = box.Text;
        Dispatcher.UIThread.Post(() =>
        {
            if (box.IsFocused && box.Text == text) box.SelectAll();
        }, DispatcherPriority.Background);
    }

    private static void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not TextBox box || box.IsFocused) return;

        e.Handled = true;
        box.Focus();
    }

    private static void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (sender is not TextBox box || string.IsNullOrEmpty(e.Text)) return;

        var pointer = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        var selected = box.SelectedText ?? string.Empty;
        var taken = (box.Text ?? string.Empty).Contains(pointer, StringComparison.Ordinal)
            && !selected.Contains(pointer, StringComparison.Ordinal);

        var builder = new StringBuilder(e.Text.Length);
        foreach (var c in e.Text)
        {
            if (char.IsDigit(c))
            {
                builder.Append(c);
            }
            else if ((c is '.' or ',' || pointer.Contains(c)) && !taken)
            {
                builder.Append(pointer);
                taken = true;
            }
        }

        var input = builder.ToString();
        if (input.Length == 0) e.Handled = true;
        else if (input != e.Text) e.Text = input;
    }

    private static void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox box || box.GetValue(BusyProperty)) return;

        var text = box.Text;
        if (string.IsNullOrEmpty(text)) return;

        var owner = box.GetValue(OwnerProperty) ?? box;
        var culture = CultureInfo.CurrentCulture.NumberFormat;
        var pointer = culture.NumberDecimalSeparator;
        var grouped = GetGroup(owner) && Applies(owner);
        var group = grouped ? culture.NumberGroupSeparator : string.Empty;

        var digitsBefore = text.Take(box.CaretIndex).Count(char.IsDigit);
        var result = GetClean(owner) ? Clean(text, group, pointer) : text;
        if (grouped && Format(result, group, pointer) is { } formatted) result = formatted;
        if (result == text) return;

        box.SetValue(BusyProperty, true);
        box.Text = result;
        box.CaretIndex = CaretFor(result, digitsBefore);
        box.SetValue(BusyProperty, false);
    }

    private static string Clean(string text, string group, string pointer)
    {
        var builder = new StringBuilder(text.Length);
        var taken = false;

        foreach (var c in text)
        {
            if (char.IsDigit(c) || group.Contains(c))
            {
                builder.Append(c);
            }
            else if ((c is '.' or ',' || pointer.Contains(c)) && !taken)
            {
                builder.Append(pointer);
                taken = true;
            }
        }

        var cleaned = builder.ToString();
        var pointerAt = cleaned.IndexOf(pointer, StringComparison.Ordinal);
        var whole = pointerAt < 0 ? cleaned : cleaned[..pointerAt];

        var trimmed = whole.TrimStart(['0', .. group]);
        if (whole.Length > 0 && trimmed.Length == 0) trimmed = "0";

        return pointerAt < 0 ? trimmed : trimmed + cleaned[pointerAt..];
    }

    private static string? Format(string text, string group, string pointer)
    {
        var sign = text.StartsWith('-') ? "-" : string.Empty;
        var body = text[sign.Length..];

        var pointerAt = body.IndexOf(pointer, StringComparison.Ordinal);
        var whole = pointerAt < 0 ? body : body[..pointerAt];
        var fraction = pointerAt < 0 ? null : body[(pointerAt + pointer.Length)..];

        if (whole.Any(c => !char.IsDigit(c) && !group.Contains(c)))
            return null;

        if (fraction is not null && fraction.Any(c => !char.IsDigit(c)))
            return null;

        var digits = new string([.. whole.Where(char.IsDigit)]);
        if (digits.Length == 0)
            return null;

        var grouped = ulong.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value.ToString("#,##0", CultureInfo.CurrentCulture)
            : digits;

        return sign + grouped + (fraction is null ? string.Empty : pointer + fraction);
    }

    private static int CaretFor(string text, int digitsBefore)
    {
        if (digitsBefore == 0) return text.TakeWhile(c => !char.IsDigit(c)).Count();

        var seen = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (!char.IsDigit(text[i])) continue;
            if (++seen == digitsBefore) return i + 1;
        }
        return text.Length;
    }
}
