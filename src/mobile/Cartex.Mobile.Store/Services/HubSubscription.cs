using Microsoft.AspNetCore.SignalR.Client;

namespace Cartex.Mobile.Store.Services;

// Hub obunasi ulanishga bog'liq: server tomonda guruh a'zoligi har qayta ulanishda
// yo'qoladi. Obunani bayroq bilan kuzatish "ulangan, lekin obuna yo'q" degan ko'rinmas
// holatga olib keladi, shuning uchun u ulanish identifikatoriga bog'lanadi - u eskirmaydi.
public sealed class HubSubscription
{
    private string? _connectionId;

    // Ulanishni ko'taradi va obunani shu ulanish uchun yangilaydi.
    // `true` - obuna endi yangilandi, ya'ni kutayotgan ishni qayta olish kerak.
    public async Task<bool> EnsureAsync(HubConnection connection, Func<Task> subscribeAsync)
    {
        if (connection.State == HubConnectionState.Disconnected)
            await connection.StartAsync();
        if (connection.ConnectionId is not { } connectionId || _connectionId == connectionId)
            return false;
        await subscribeAsync();
        _connectionId = connectionId;
        return true;
    }

    public void Invalidate() => _connectionId = null;
}
