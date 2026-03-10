using System.ComponentModel;
using Cartex.UI.Models;

namespace Cartex.UI.Services;

public sealed class ThemeManager : INotifyPropertyChanged
{
    public static ThemeManager Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<AppTheme>? ThemeChanged;

    private AppTheme _theme;
    public AppTheme Theme
    {
        get => _theme;
        set
        {
            if (_theme == value) return;
            _theme = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Theme)));
            ThemeChanged?.Invoke(value);
        }
    }

    private ThemeManager() { }
}
