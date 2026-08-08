using Material.Icons;

namespace Cartex.UI.Models;

public sealed class PaletteItem(string title, MaterialIconKind icon, Action invoke)
{
    public string Title { get; } = title;
    public MaterialIconKind Icon { get; } = icon;
    public Action Invoke { get; } = invoke;
}
