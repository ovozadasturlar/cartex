using Microsoft.AspNetCore.SignalR;

namespace Cartex.Api.Hubs;

/// Kanalga obuna bo'ladigan har bir hub shu asosdan quriladi: guruh a'zoligi va
/// "kim tinglayapti" ro'yxati doim birga o'zgaradi. Uzilishni qo'lda kuzatish kerak
/// emas - uni unutish marshrutlash uchun ko'rinmas nosozlik bo'lardi.
public abstract class PresenceHub(HubPresence presence) : Hub
{
    protected async Task JoinAsync(string channel)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, channel);
        presence.Join(Context.ConnectionId, channel);
    }

    protected async Task LeaveAsync(string channel)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, channel);
        presence.Leave(Context.ConnectionId, channel);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var channels = presence.LeaveAll(Context.ConnectionId);
        if (channels.Count > 0)
            await OnChannelsLeftAsync(channels);
        await base.OnDisconnectedAsync(exception);
    }

    /// Qurilmaning "oxirgi ko'ringan" belgisi kabi hub'ga xos yakuniy ish shu yerda bajariladi.
    protected virtual Task OnChannelsLeftAsync(IReadOnlyList<string> channels) => Task.CompletedTask;
}
