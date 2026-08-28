using Cartex.Mobile.Core;
using Microsoft.AspNetCore.SignalR.Client;

namespace Cartex.Mobile.Store.Services;

public sealed class OrderingHubService(SessionStore session, MobileAuthService auth)
{
    private readonly SemaphoreSlim _startLock = new(1, 1);
    private HubConnection? _connection;
    private readonly HubSubscription _subscription = new();

    public event Action<string>? CartsChanged;
    public event Action? Resynced;

    public async Task EnsureStartedAsync()
    {
        if (auth.DefaultBranchId is not long branchId) return;
        await _startLock.WaitAsync();
        try
        {
            var connection = _connection ??= Build();
            await _subscription.EnsureAsync(connection, () => connection.InvokeAsync("Subscribe", branchId));
        }
        catch
        {
            _subscription.Invalidate();
        }
        finally { _startLock.Release(); }
    }

    private HubConnection Build()
    {
        var connection = MobileHubConnections.Create("/hubs/ordering", session, auth);
        connection.On<string>("CartsChanged", kind => MainThread.BeginInvokeOnMainThread(() => CartsChanged?.Invoke(kind)));
        connection.Reconnected += async _ =>
        {
            _subscription.Invalidate();
            await EnsureStartedAsync();
            MainThread.BeginInvokeOnMainThread(() => Resynced?.Invoke());
        };
        connection.Closed += async _ =>
        {
            // Chiqishda ulanishni o'zimiz tashlaymiz - o'shanda qayta ko'tarmaslik kerak.
            if (!ReferenceEquals(_connection, connection)) return;
            _subscription.Invalidate();
            await EnsureStartedAsync();
        };
        return connection;
    }

    public async Task StopAsync()
    {
        await _startLock.WaitAsync();
        var connection = _connection;
        _connection = null;
        _subscription.Invalidate();
        _startLock.Release();
        if (connection is null) return;
        try { await connection.DisposeAsync(); } catch { }
    }
}
