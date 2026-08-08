using Avalonia.Threading;
using Microsoft.AspNetCore.SignalR.Client;

namespace Cartex.UI.Services;

public sealed class QueueHubService
{
    private readonly AuthService _auth;
    private readonly SemaphoreSlim _startLock = new(1, 1);
    private HubConnection? _connection;

    public event Action<string>? CartsChanged;
    public event Action? Resynced;

    public QueueHubService(AuthService auth)
    {
        _auth = auth;
        _auth.LoggedOut += () => _ = StopAsync();
    }

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
        finally
        {
            _startLock.Release();
        }
    }

    private HubConnection Build()
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(SettingsService.Instance.ApiBaseUrl.TrimEnd('/') + "/hubs/ordering",
                o => o.AccessTokenProvider = () => _auth.EnsureFreshTokenAsync(CancellationToken.None))
            .WithAutomaticReconnect()
            .Build();
        connection.On<string>("CartsChanged", kind => Dispatcher.UIThread.Post(() => CartsChanged?.Invoke(kind)));
        connection.Reconnected += _ =>
        {
            Dispatcher.UIThread.Post(() => Resynced?.Invoke());
            return Task.CompletedTask;
        };
        return connection;
    }

    private async Task StopAsync()
    {
        await _startLock.WaitAsync();
        var connection = _connection;
        _connection = null;
        _startLock.Release();
        if (connection is null) return;
        try { await connection.DisposeAsync(); } catch { }
    }
}
