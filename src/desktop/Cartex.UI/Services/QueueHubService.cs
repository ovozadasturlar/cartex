using System.ComponentModel;
using Avalonia.Threading;
using Microsoft.AspNetCore.SignalR.Client;

namespace Cartex.UI.Services;

public sealed class QueueHubService
{
    private readonly AuthService _auth;
    private readonly BranchContextService _branch;
    private readonly SemaphoreSlim _startLock = new(1, 1);
    private HubConnection? _connection;
    private readonly HubSubscription _subscription = new();
    private long? _subscribedBranchId;

    public event Action<string>? CartsChanged;
    public event Action? Resynced;

    public QueueHubService(AuthService auth, BranchContextService branch)
    {
        _auth = auth;
        _branch = branch;
        _auth.LoggedOut += () => _ = StopAsync();
        _branch.PropertyChanged += BranchChanged;
    }

    public async Task EnsureStartedAsync()
    {
        if (_branch.CurrentBranchId is not long branchId) return;
        await _startLock.WaitAsync();
        try
        {
            var connection = _connection ??= Build();
            // Filial almashsa obuna ham yangilanishi kerak, ulanish o'zgarmagan bo'lsa ham.
            if (_subscribedBranchId != branchId) _subscription.Invalidate();
            if (await _subscription.EnsureAsync(connection, () => connection.InvokeAsync("Subscribe", branchId)))
                _subscribedBranchId = branchId;
        }
        catch
        {
            _subscription.Invalidate();
        }
        finally
        {
            _startLock.Release();
        }
    }

    private HubConnection Build()
    {
        var connection = HubConnections.Create("/hubs/ordering", _auth);
        connection.On<string>("CartsChanged", kind => Dispatcher.UIThread.Post(() => CartsChanged?.Invoke(kind)));
        connection.Reconnected += async _ =>
        {
            _subscription.Invalidate();
            await EnsureStartedAsync();
            Dispatcher.UIThread.Post(() => Resynced?.Invoke());
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

    private void BranchChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(BranchContextService.SelectedBranch))
            _ = EnsureStartedAsync();
    }

    private async Task StopAsync()
    {
        await _startLock.WaitAsync();
        var connection = _connection;
        _connection = null;
        _subscription.Invalidate();
        _subscribedBranchId = null;
        _startLock.Release();
        if (connection is null) return;
        try { await connection.DisposeAsync(); } catch { }
    }
}
