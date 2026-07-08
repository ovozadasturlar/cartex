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
    public async Task<bool> ConfirmAsync(string message, string? title = null) =>
        await MessageBox.ShowAsync(message, title, MessageBoxIcon.Question, MessageBoxButton.YesNo) == MessageBoxResult.Yes;

    public async Task<bool> ConfirmDangerAsync(string message, string? title = null) =>
        await MessageBox.ShowAsync(message, title, MessageBoxIcon.Warning, MessageBoxButton.YesNo) == MessageBoxResult.Yes;

    public Task AlertAsync(string message, string? title = null) =>
        MessageBox.ShowAsync(message, title, MessageBoxIcon.Information, MessageBoxButton.OK);

    public Task<TResult?> ShowAsync<TView, TViewModel, TResult>(TViewModel vm) where TView : Control, new() =>
        OverlayDialog.ShowCustomModal<TView, TViewModel, TResult>(vm);
}
