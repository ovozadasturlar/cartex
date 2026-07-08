using Avalonia.Input;

namespace Cartex.UI.Models;

public sealed record PageShortcut(Key Key, KeyModifiers Modifiers, string LabelKey, Action Execute, Func<bool>? IsEnabled = null, bool WorksInText = false)
{
    public string Gesture => GestureText(Key, Modifiers);

    public static string GestureText(Key key, KeyModifiers modifiers)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(KeyModifiers.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");
        parts.Add(key switch
        {
            Key.Return => "Enter",
            Key.Delete => "Del",
            Key.Escape => "Esc",
            Key.Space => "Space",
            Key.Add => "Num +",
            Key.Subtract => "Num −",
            _ => key.ToString()
        });
        return string.Join("+", parts);
    }
}
