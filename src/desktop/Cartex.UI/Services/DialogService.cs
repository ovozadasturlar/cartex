using Avalonia.Controls;
using Ursa.Controls;

namespace Cartex.UI.Services;

public interface IDialogService
{
    Task<bool> ConfirmAsync(string message, string? title = null);
    Task<bool> ConfirmDangerAsync(string message, string? title = null);
    Task AlertAsync(string message, string? title = null);
    Task<TResult?> ShowAsync<TView, TViewModel, TResult>(TViewModel vm) where TView : Control, new();
    Task<string?> PromptAsync(string title, string? message = null, string? placeholder = null);
    void CloseOverlay();
}

public sealed class DialogService : IDialogService
{
    public event Action<bool>? OpenChanged;

    /// <summary>
    /// Dialogs are shown in the named shell host. Ursa resolves an unnamed host by
    /// registration order, so a second host would silently steal every dialog.
    /// </summary>
    public const string HostId = "app";

    private readonly Lock _gate = new();
    private readonly List<CancellationTokenSource> _open = [];
    private int _openCount;

    private async Task<T> TrackAsync<T>(Task<T> task)
    {
        if (Interlocked.Increment(ref _openCount) == 1) OpenChanged?.Invoke(true);
        try { return await task; }
        finally { if (Interlocked.Decrement(ref _openCount) == 0) OpenChanged?.Invoke(false); }
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
        lock (_gate) _open.Add(cancellation);

        try
        {
            return await TrackAsync(OverlayDialog.ShowCustomAsync<TView, TViewModel, TResult>(
                vm,
                hostId: HostId,
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
            lock (_gate) _open.Remove(cancellation);
            cancellation.Dispose();
        }
    }

    public Task<string?> PromptAsync(string title, string? message = null, string? placeholder = null) =>
        ShowAsync<Views.PromptDialog, ViewModels.PromptViewModel, string>(
            new ViewModels.PromptViewModel(title, message, placeholder));

    public void CloseOverlay()
    {
        CancellationTokenSource? top;
        lock (_gate) top = _open.Count > 0 ? _open[^1] : null;
        if (top is null) return;
        try { top.Cancel(); }
        catch (ObjectDisposedException) { }
    }
}
