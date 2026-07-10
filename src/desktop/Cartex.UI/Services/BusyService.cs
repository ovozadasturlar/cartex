using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Cartex.UI.Services;

public interface IBusyService
{
    bool IsBusy { get; }
    string? Message { get; }
    IDisposable Begin(string? message = null);
}

public partial class BusyService : ObservableObject, IBusyService
{
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _message;
    private int _count;
    private DispatcherTimer? _showTimer;

    public IDisposable Begin(string? message = null)
    {
        _count++;
        Message = message;
        if (_count == 1 && !IsBusy)
        {
            _showTimer?.Stop();
            _showTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _showTimer.Tick += (_, _) =>
            {
                _showTimer?.Stop();
                if (_count > 0) IsBusy = true;
            };
            _showTimer.Start();
        }
        return new Scope(this);
    }

    private void End()
    {
        if (--_count > 0) return;
        _count = 0;
        _showTimer?.Stop();
        _showTimer = null;
        IsBusy = false;
        Message = null;
    }

    private sealed class Scope(BusyService owner) : IDisposable
    {
        public void Dispose() => owner.End();
    }
}
