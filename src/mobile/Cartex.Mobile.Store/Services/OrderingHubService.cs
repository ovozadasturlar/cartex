using Cartex.Mobile.Core;
using Microsoft.AspNetCore.SignalR.Client;

namespace Cartex.Mobile.Store.Services;

public sealed class OrderingHubService(SessionStore session, MobileAuthService auth)
{
    private readonly SemaphoreSlim _startLock = new(1, 1);
    private HubConnection? _connection;

    public event Action<string>? CartsChanged;
    public event Action? Resynced;

    public async Task EnsureStartedAsync()
    {
        await _startLock.WaitAsync();
        try
        {
            _connection ??= Build();
            if (_connection.State == HubConnectionState.Disconnected)
                await _connection.StartAsync();
        }
        catch { }
        finally { _startLock.Release(); }
    }

    private HubConnection Build()
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(session.ServerUrl.TrimEnd('/') + "/hubs/ordering",
                options => options.AccessTokenProvider = () => auth.EnsureFreshTokenAsync(CancellationToken.None))
            .WithAutomaticReconnect()
            .Build();
        connection.On<string>("CartsChanged", kind => MainThread.BeginInvokeOnMainThread(() => CartsChanged?.Invoke(kind)));
        connection.Reconnected += _ =>
        {
            MainThread.BeginInvokeOnMainThread(() => Resynced?.Invoke());
            return Task.CompletedTask;
        };
        return connection;
    }

    public async Task StopAsync()
    {
        await _startLock.WaitAsync();
        var connection = _connection;
        _connection = null;
        _startLock.Release();
        if (connection is null) return;
        try { await connection.DisposeAsync(); } catch { }
    }
}
