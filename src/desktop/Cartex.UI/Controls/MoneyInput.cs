using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace Cartex.UI.Controls;

public static class MoneyInput
{
    public static readonly AttachedProperty<bool> GroupProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("Group", typeof(MoneyInput));

    public static bool GetGroup(Control c) => c.GetValue(GroupProperty);
    public static void SetGroup(Control c, bool value) => c.SetValue(GroupProperty, value);

    private static readonly AttachedProperty<bool> BusyProperty =
        AvaloniaProperty.RegisterAttached<TextBox, bool>("Busy", typeof(MoneyInput));

    static MoneyInput()
    {
        GroupProperty.Changed.AddClassHandler<Control>((control, e) =>
        {
            if (control is TextBox box)
            {
                box.TextChanged -= OnTextChanged;
                if (e.NewValue is true) box.TextChanged += OnTextChanged;
                return;
            }

            if (control is not TemplatedControl templated) return;

            templated.TemplateApplied -= OnTemplateApplied;
            Detach(templated);
            if (e.NewValue is true)
            {
                templated.TemplateApplied += OnTemplateApplied;
                Attach(templated);
            }
        });
    }

    private static void OnTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        if (sender is Control control) Attach(control);
    }

    private static void Attach(Control control)
    {
        if (!Applies(control) || InnerBox(control) is not { } box) return;
        box.TextChanged -= OnTextChanged;
        box.TextChanged += OnTextChanged;
    }

    private static void Detach(Control control)
    {
        if (InnerBox(control) is { } box) box.TextChanged -= OnTextChanged;
    }

    private static bool Applies(Control control) =>
        control is not NumericUpDown numeric
        || (numeric.FormatString is { Length: > 0 } format
            && (format.StartsWith('N') || format.StartsWith('n') || format.Contains("#,#")));

    private static TextBox? InnerBox(Control control) =>
        control as TextBox ?? control.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();

    private static void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox box || box.GetValue(BusyProperty)) return;

        var text = box.Text;
        if (string.IsNullOrEmpty(text)) return;

        var culture = CultureInfo.CurrentCulture.NumberFormat;
        var group = culture.NumberGroupSeparator;
        var pointer = culture.NumberDecimalSeparator;

        var digitsBefore = text.Take(box.CaretIndex).Count(char.IsDigit);
        var formatted = Format(text, group, pointer);
        if (formatted is null || formatted == text) return;

        box.SetValue(BusyProperty, true);
        box.Text = formatted;
        box.CaretIndex = CaretFor(formatted, digitsBefore);
        box.SetValue(BusyProperty, false);
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
