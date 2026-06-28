using Material.Icons;
using Avalonia.Controls;

namespace Cartex.UI.Services;

public static class LayoutCycleEvent
{
    public static int LayoutMode { get; set; }
    public static string[] PanelSlots { get; set; } = ["products", "cart", "payment"];
    public static GridLength[]? SavedColumnWidths { get; set; }
    public static GridLength[]? SavedRowHeights { get; set; }
    public static int SavedLayoutMode { get; set; } = -1;

    public static int TouchLayoutMode { get; set; }
    public static string[] TouchPanelSlots { get; set; } = ["t-products", "t-cart", "t-payment"];
    public static GridLength[]? TouchSavedColumnWidths { get; set; }
    public static GridLength[]? TouchSavedRowHeights { get; set; }
    public static int TouchSavedLayoutMode { get; set; } = -1;

    public static event Action? Requested;
    public static event Action<MaterialIconKind>? IconChanged;

    public static void Raise() => Requested?.Invoke();
    public static void NotifyIconChanged(MaterialIconKind kind) => IconChanged?.Invoke(kind);
}
