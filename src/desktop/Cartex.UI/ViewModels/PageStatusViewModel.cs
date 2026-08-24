using CommunityToolkit.Mvvm.Input;

namespace Cartex.UI.ViewModels;

public sealed partial class PageStatusViewModel(
    string title,
    string message,
    bool isLoading,
    Func<Task>? retry = null) : ViewModelBase
{
    public string Title { get; } = title;
    public string Message { get; } = message;
    public bool IsLoading { get; } = isLoading;
    public bool HasError => !IsLoading;

    [RelayCommand]
    private async Task RetryAsync()
    {
        if (retry is not null)
            await retry();
    }
}
