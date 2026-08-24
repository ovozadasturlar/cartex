using Cartex.Mobile.Core;
using Cartex.Shared.Models.Settings;
using Xunit;

namespace Cartex.UnitTests;

public sealed class AccessStateTests
{
    [Fact]
    public void RUXSAT_04_Unloaded_state_denies_access()
    {
        var state = new AccessState(new FakeLoader());

        Assert.False(state.IsLoaded);
        Assert.Equal(AccessLoadState.Loading, state.State);
        Assert.False(state.Has("sales.create"));
        Assert.False(state.Feature("ordering"));
    }

    [Fact]
    public async Task WP20_failed_load_has_distinct_state_and_denies_access()
    {
        var state = new AccessState(new FailingLoader(new HttpRequestException("offline")));

        await state.EnsureLoadedAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AccessLoadState.Failed, state.State);
        Assert.Equal(AccessFailureKind.Network, state.FailureKind);
        Assert.False(state.IsLoaded);
        Assert.False(state.Has("sales.create"));
    }

    [Fact]
    public async Task WP20_manual_retry_recovers_failed_access_load()
    {
        var loader = new RecoveringLoader(Snapshot(["sales.create"], ["ordering"]));
        var state = new AccessState(loader);
        await state.EnsureLoadedAsync(TestContext.Current.CancellationToken);

        await state.RefreshAsync();

        Assert.Equal(AccessLoadState.Loaded, state.State);
        Assert.True(state.CanSell);
        Assert.Equal(2, loader.LoadCount);
    }

    [Fact]
    public async Task RUXSAT_04_EnsureLoaded_exposes_loaded_access()
    {
        var loader = new FakeLoader(Snapshot(["sales.create"], ["ordering"]));
        var state = new AccessState(loader);

        await state.EnsureLoadedAsync(TestContext.Current.CancellationToken);

        Assert.True(state.IsLoaded);
        Assert.True(state.Has("sales.create"));
        Assert.True(state.CartsEnabled);
    }

    [Fact]
    public async Task RUXSAT_04_Ten_concurrent_ensures_share_one_load()
    {
        var loader = new FakeLoader(Snapshot(["sales.create"], ["store"]));
        var state = new AccessState(loader);

        await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(_ => state.EnsureLoadedAsync(TestContext.Current.CancellationToken)));

        Assert.Equal(1, loader.LoadCount);
    }

    [Fact]
    public async Task RUXSAT_04_Changed_fires_once_for_one_state_change()
    {
        var state = new AccessState(new FakeLoader(Snapshot(["sales.create"], ["ordering"])));
        var changes = 0;
        state.Changed += () => changes++;

        await Task.WhenAll(
            state.EnsureLoadedAsync(TestContext.Current.CancellationToken),
            state.EnsureLoadedAsync(TestContext.Current.CancellationToken));

        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task RUXSAT_04_Login_refreshes_permissions_and_logout_clears_them()
    {
        var loader = new FakeLoader(
            Snapshot(["sales.view"], []),
            Snapshot(["sales.create"], ["ordering"]));
        var state = new AccessState(loader);
        await state.EnsureLoadedAsync(TestContext.Current.CancellationToken);

        await state.RefreshAsync();

        Assert.False(state.Has("sales.view"));
        Assert.True(state.Has("sales.create"));
        state.Clear();
        Assert.False(state.IsLoaded);
        Assert.False(state.Has("sales.create"));
    }

    [Fact]
    public async Task RUXSAT_04_Logout_clear_wins_over_an_in_flight_load()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var loader = new FakeLoader(Snapshot(["sales.create"], ["ordering"]))
        {
            BeforeReturn = () => release.Task
        };
        var state = new AccessState(loader);
        var loading = state.EnsureLoadedAsync(TestContext.Current.CancellationToken);
        await loader.Started.Task;

        state.Clear();
        release.SetResult();
        await loading;

        Assert.False(state.IsLoaded);
        Assert.False(state.Has("sales.create"));
    }

    private static AccessSnapshot Snapshot(string[] permissions, string[] features) => new(
        permissions.ToHashSet(StringComparer.Ordinal),
        features.ToHashSet(StringComparer.OrdinalIgnoreCase),
        new SalesPolicyDto(),
        DateTimeOffset.UtcNow);

    private sealed class FakeLoader(params AccessSnapshot[] snapshots) : IAccessStateLoader
    {
        private readonly Queue<AccessSnapshot> _snapshots = new(snapshots);
        private AccessSnapshot _last = snapshots.LastOrDefault()
            ?? Snapshot([], []);

        public int LoadCount { get; private set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Func<Task>? BeforeReturn { get; init; }
        public bool IsOnline => true;
        public event Action? ConnectivityChanged
        {
            add { }
            remove { }
        }

        public async Task<AccessSnapshot> LoadAsync(bool refresh, CancellationToken cancellationToken)
        {
            LoadCount++;
            Started.TrySetResult();
            if (BeforeReturn is not null) await BeforeReturn();
            await Task.Yield();
            if (_snapshots.TryDequeue(out var snapshot)) _last = snapshot;
            return _last;
        }

        public void Clear() { }
    }

    private sealed class FailingLoader(Exception exception) : IAccessStateLoader
    {
        public bool IsOnline => true;
        public event Action? ConnectivityChanged
        {
            add { }
            remove { }
        }

        public Task<AccessSnapshot> LoadAsync(bool refresh, CancellationToken cancellationToken) =>
            Task.FromException<AccessSnapshot>(exception);

        public void Clear() { }
    }

    private sealed class RecoveringLoader(AccessSnapshot snapshot) : IAccessStateLoader
    {
        public int LoadCount { get; private set; }
        public bool IsOnline => true;
        public event Action? ConnectivityChanged
        {
            add { }
            remove { }
        }

        public Task<AccessSnapshot> LoadAsync(bool refresh, CancellationToken cancellationToken)
        {
            LoadCount++;
            return LoadCount == 1
                ? Task.FromException<AccessSnapshot>(new HttpRequestException("offline"))
                : Task.FromResult(snapshot);
        }

        public void Clear() { }
    }
}
