using System.ComponentModel;
using Cartex.UI.Models;

namespace Cartex.UI.Services;

public sealed class TouchModeManager : INotifyPropertyChanged
{
    public static TouchModeManager Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    private AppMode _mode;
    public AppMode Mode
    {
        get => _mode;
        set
        {
            if (_mode == value) return;
            _mode = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Mode)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsTouchMode)));
        }
    }

    public bool IsTouchMode => Mode == AppMode.Touch;
}
