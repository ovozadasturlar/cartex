using Avalonia.Controls;
using Ursa.Controls;

namespace Cartex.UI.Services;

public interface IDialogService
{
    Task<bool> ConfirmAsync(string message, string? title = null);
    Task<bool> ConfirmDangerAsync(string message, string? title = null);
    Task AlertAsync(string message, string? title = null);
    Task<TResult?> ShowAsync<TView, TViewModel, TResult>(TViewModel vm) where TView : Control, new();
}

public sealed class DialogService : IDialogService
{
    public event Action<bool>? OpenChanged;
    private int _openCount;

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

    public Task<TResult?> ShowAsync<TView, TViewModel, TResult>(TViewModel vm) where TView : Control, new() =>
        TrackAsync(OverlayDialog.ShowCustomAsync<TView, TViewModel, TResult>(vm));
}
