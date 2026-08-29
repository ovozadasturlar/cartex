using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Cartex.UI.Services;

public enum ScanIndicatorState { Hidden, Busy, Found, Missing }

public partial class ScanIndicator : ObservableObject
{
    private static readonly TimeSpan ShowDelay = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan ResultDuration = TimeSpan.FromMilliseconds(1400);

    private readonly DispatcherTimer _timer = new();
    private int _generation;

    [ObservableProperty] private ScanIndicatorState _state = ScanIndicatorState.Hidden;

    public bool IsVisible => State != ScanIndicatorState.Hidden;
    public bool IsBusy => State == ScanIndicatorState.Busy;
    public bool IsFound => State == ScanIndicatorState.Found;
    public bool IsMissing => State == ScanIndicatorState.Missing;

    public ScanIndicator() => _timer.Tick += (_, _) => Hide();

    partial void OnStateChanged(ScanIndicatorState value)
    {
        OnPropertyChanged(nameof(IsVisible));
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(IsFound));
        OnPropertyChanged(nameof(IsMissing));
    }

    public async Task<T> TrackAsync<T>(Task<T> lookup, Func<T, bool>? found = null)
    {
        var generation = ++_generation;
        _timer.Stop();
        State = ScanIndicatorState.Hidden;

        var slow = await Task.WhenAny(lookup, Task.Delay(ShowDelay)) != lookup;
        if (slow && generation == _generation) State = ScanIndicatorState.Busy;

        try
        {
            var result = await lookup;
            var hit = found is null ? result is not null : found(result);
            if (slow && generation == _generation)
                Show(hit ? ScanIndicatorState.Found : ScanIndicatorState.Missing);
            return result;
        }
        catch
        {
            if (generation == _generation) Hide();
            throw;
        }
    }

    private void Show(ScanIndicatorState state)
    {
        State = state;
        _timer.Interval = ResultDuration;
        _timer.Start();
    }

    private void Hide()
    {
        _timer.Stop();
        State = ScanIndicatorState.Hidden;
    }
}
