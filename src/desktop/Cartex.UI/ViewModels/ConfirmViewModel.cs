using CommunityToolkit.Mvvm.Input;
using Irihi.Avalonia.Shared.Contracts;

namespace Cartex.UI.ViewModels;

/// Ursa'ning tayyor MessageBox'i tugmalarini o'z tilidan oladi va ular ingliz tilida qoladi —
/// o'zbekcha ekranda "Yes/No" chiqadi. Tasdiq oynasi ilovaning o'z naqshida yozilgan, shunda
/// matn ham til fayllaridan keladi, ko'rinish ham qolgan dialoglar bilan bir xil bo'ladi.
public partial class ConfirmViewModel(string message, string? title, bool danger, bool alertOnly)
    : ViewModelBase, IDialogContext
{
    public string Message { get; } = message;
    public string? Title { get; } = title;
    public bool HasTitle => !string.IsNullOrWhiteSpace(Title);
    public bool IsDanger { get; } = danger;
    public bool ShowCancel { get; } = !alertOnly;

    public event EventHandler<object?>? RequestClose;

    [RelayCommand]
    private void Accept() => RequestClose?.Invoke(this, true);

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(this, false);

    public void Close() => RequestClose?.Invoke(this, false);
}
