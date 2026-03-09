using System.ComponentModel;

namespace Cartex.UI.Services;

public sealed class TouchModeManager : INotifyPropertyChanged
{
    public static TouchModeManager Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool _isTouchMode;
    public bool IsTouchMode
    {
        get => _isTouchMode;
        set
        {
            if (_isTouchMode == value) return;
            _isTouchMode = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsTouchMode)));
        }
    }
}
