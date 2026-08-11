using System.Collections.Concurrent;
using Avalonia.Controls;
using Ursa.Controls;

namespace Cartex.UI.Services;

public interface IDialogService
{
    Task<bool> ConfirmAsync(string message, string? title = null);
    Task<bool> ConfirmDangerAsync(string message, string? title = null);
    Task AlertAsync(string message, string? title = null);
    Task<TResult?> ShowAsync<TView, TViewModel, TResult>(TViewModel vm) where TView : Control, new();
    void CloseOverlay();
}

public sealed class DialogService : IDialogService
{
    public event Action<bool>? OpenChanged;
    private int _openCount;
    private readonly ConcurrentStack<CancellationTokenSource> _cancellations = new();

    private async Task<T> TrackAsync<T>(Task<T> task)
    {
        if (++_openCount == 1) OpenChanged?.Invoke(true);
        try { return await task; }
        finally { if (--_openCount == 0) OpenChanged?.Invoke(false); }
    }

    public async Task<bool> ConfirmAsync(string message, string? title = null) =>
        await TrackAsync(MessageBox.ShowAsync(message, title, MessageBoxIcon.Question, MessageBoxButton.YesNo)) == MessageBoxResult.Yes;

    public async Task<bool> ConfirmDangerAsync(string message, string? title = null) =>
        await TrackAsync(MessageBox.ShowAsync(message, title, MessageBoxIcon.Warning, MessageBoxButton.YesNo)) == MessageBoxResult.Yes;

    public async Task AlertAsync(string message, string? title = null) =>
        await TrackAsync(MessageBox.ShowAsync(message, title, MessageBoxIcon.Information, MessageBoxButton.OK));

    public async Task<TResult?> ShowAsync<TView, TViewModel, TResult>(TViewModel vm) where TView : Control, new()
    {
        var cancellation = new CancellationTokenSource();
        _cancellations.Push(cancellation);

        try
        {
            return await TrackAsync(OverlayDialog.ShowCustomAsync<TView, TViewModel, TResult>(
                vm,
                options: new OverlayDialogOptions 
                { 
                    CanLightDismiss = true,
                    Buttons = DialogButton.None
                },
                token: cancellation.Token));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return default;
        }
        finally
        {
            // Remove our cancellation from stack
            var remaining = new System.Collections.Generic.List<CancellationTokenSource>();
            while (_cancellations.TryPop(out var item))
            {
                if (item == cancellation) break;
                remaining.Add(item);
            }
            for (int i = remaining.Count - 1; i >= 0; i--)
            {
                _cancellations.Push(remaining[i]);
            }
            cancellation.Dispose();
        }
    }

    public void CloseOverlay()
    {
        if (_cancellations.TryPeek(out var top))
        {
            top.Cancel();
        }
    }
}
