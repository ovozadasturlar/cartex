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

    public IDisposable Begin(string? message = null)
    {
        _count++;
        Message = message;
        IsBusy = true;
        return new Scope(this);
    }

    private void End()
    {
        if (--_count > 0) return;
        _count = 0;
        IsBusy = false;
        Message = null;
    }

    private sealed class Scope(BusyService owner) : IDisposable
    {
        public void Dispose() => owner.End();
    }
}
