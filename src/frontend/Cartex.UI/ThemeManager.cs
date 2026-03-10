using Cartex.UI.Models;

namespace Cartex.UI;

public static class ThemeManager
{
    public static Action<AppTheme>? ApplyTheme { get; set; }
}
