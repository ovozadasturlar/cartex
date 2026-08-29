using Cartex.UI.ViewModels;
using Xunit;

namespace Cartex.UnitTests;

public sealed class DesktopBootstrapTests
{
    [Fact]
    public async Task WP20_landing_is_selected_only_after_features_and_branch_finish()
    {
        var features = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var branch = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var selected = false;
        var loading = MainViewModel.CompleteBootstrapAsync(
            features.Task,
            branch.Task,
            () => selected = true);

        features.SetResult();
        await Task.Yield();
        Assert.False(selected);

        branch.SetResult();
        await loading;

        Assert.True(selected);
    }

    [Fact]
    public async Task WP20_only_successfully_loaded_page_is_reused_after_menu_rebuild()
    {
        var page = new TestPage();

        Assert.False(MainViewModel.CanReusePage("dashboard", "dashboard", typeof(TestPage), page));

        await MainViewModel.LoadPageAsync(page);

        Assert.Equal(PageLoadState.Loaded, page.LoadState);
        Assert.True(MainViewModel.CanReusePage("dashboard", "dashboard", typeof(TestPage), page));
    }

    [Fact]
    public async Task WP20_cancelled_page_load_is_not_marked_loaded()
    {
        var page = new TestPage { Failure = new OperationCanceledException() };

        await Assert.ThrowsAsync<OperationCanceledException>(() => MainViewModel.LoadPageAsync(page));

        Assert.Equal(PageLoadState.NotLoaded, page.LoadState);
    }

    private sealed class TestPage : ViewModelBase, ILoadable
    {
        public Exception? Failure { get; init; }

        public Task LoadAsync() => Failure is null
            ? Task.CompletedTask
            : Task.FromException(Failure);
    }
}
