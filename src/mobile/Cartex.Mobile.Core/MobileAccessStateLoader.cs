namespace Cartex.Mobile.Core;

public sealed class MobileAccessStateLoader(
    SessionStore session,
    MobilePermissions permissions,
    MobileFeaturesCache features,
    SalesPolicyCache salesPolicy) : IAccessStateLoader
{
    private const string LastRefreshKey = "access_last_refresh";

    public bool IsOnline => Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

    public event Action? ConnectivityChanged;

    public async Task<AccessSnapshot> LoadAsync(bool refresh, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await session.LoadAsync();
        if (session.AccessToken is null)
            throw new UnauthorizedAccessException();

        if (refresh)
            await Task.WhenAll(features.RefreshAsync(), salesPolicy.RefreshAsync());
        else
            await Task.WhenAll(features.EnsureLoadedAsync(), salesPolicy.EnsureLoadedAsync());

        if ((!features.LastRefreshSucceeded || !salesPolicy.LastRefreshSucceeded)
            && (!features.HasCachedData || !salesPolicy.HasCachedData))
            throw features.LastRefreshError ?? salesPolicy.LastRefreshError ?? new HttpRequestException();

        var lastRefresh = ReadLastRefresh();
        if (features.LastRefreshSucceeded && salesPolicy.LastRefreshSucceeded)
        {
            lastRefresh = DateTimeOffset.UtcNow;
            Preferences.Set(LastRefreshKey, lastRefresh.Value.ToString("O"));
        }

        return new AccessSnapshot(
            permissions.Snapshot(),
            features.Current.ToHashSet(StringComparer.OrdinalIgnoreCase),
            salesPolicy.Current,
            lastRefresh);
    }

    public void StartConnectivityWatch() =>
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;

    public void Clear()
    {
        features.Clear();
        salesPolicy.Clear();
        Preferences.Remove(LastRefreshKey);
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs args) =>
        ConnectivityChanged?.Invoke();

    private static DateTimeOffset? ReadLastRefresh() =>
        DateTimeOffset.TryParse(Preferences.Get(LastRefreshKey, ""), out var value) ? value : null;
}
