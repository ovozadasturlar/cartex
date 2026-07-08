using Avalonia;
using Avalonia.Controls;
using Cartex.UI.Models;
using Cartex.UI.Services;

namespace Cartex.UI.Controls;

public static class Shortcuts
{
    public static readonly AttachedProperty<IReadOnlyList<PageShortcut>?> SourceProperty =
        AvaloniaProperty.RegisterAttached<Control, IReadOnlyList<PageShortcut>?>("Source", typeof(Shortcuts));

    public static IReadOnlyList<PageShortcut>? GetSource(Control control) => control.GetValue(SourceProperty);
    public static void SetSource(Control control, IReadOnlyList<PageShortcut>? value) => control.SetValue(SourceProperty, value);

    static Shortcuts()
    {
        SourceProperty.Changed.AddClassHandler<Control>((control, args) =>
        {
            control.AttachedToVisualTree -= OnAttached;
            control.DetachedFromVisualTree -= OnDetached;
            if (args.NewValue is null) return;
            control.AttachedToVisualTree += OnAttached;
            control.DetachedFromVisualTree += OnDetached;
            if (control.IsLoaded) Apply(control);
        });
    }

    private static void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is Control control) Apply(control);
    }

    private static void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is Control control) ServiceLocator.Resolve<ShortcutService>().ClearScope(control);
    }

    private static void Apply(Control control)
    {
        if (GetSource(control) is { } shortcuts)
            ServiceLocator.Resolve<ShortcutService>().SetScope(control, shortcuts);
    }
}
