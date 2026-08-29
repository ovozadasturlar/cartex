using Cartex.Application.Common.Interfaces;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Api.Hubs;

[Authorize]
public sealed class PrintingHub(IApplicationDbContext db, ICurrentUser currentUser, HubPresence presence)
    : PresenceHub(presence)
{
    private const string DeviceKey = "print-device";

    public async Task Subscribe(string deviceId, string hostToken)
    {
        if (!currentUser.HasPermission(AppPermissions.Printing.Host))
            throw new HubException("Print host permission is required.");
        if (!string.IsNullOrWhiteSpace(currentUser.DeviceId) && currentUser.DeviceId != deviceId)
            throw new HubException("Device identity mismatch.");
        var node = await db.PrintNodes.FirstOrDefaultAsync(x => x.DeviceId == deviceId
            && x.IsTrusted && x.HostEnabled && x.LastUserId == currentUser.UserId);
        if (node is null) throw new HubException("Print node is not registered.");
        try { Cartex.Application.Printing.PrintingCredential.Ensure(node, hostToken); }
        catch { throw new HubException("Invalid print host credential."); }
        await JoinAsync(HubChannels.PrintHost(deviceId));
        Context.Items[DeviceKey] = deviceId;
    }

    public Task SubscribeRequester(string deviceId)
    {
        if (!currentUser.HasPermission(AppPermissions.Printing.RemoteUse))
            throw new HubException("Remote printing permission is required.");
        if (string.IsNullOrWhiteSpace(currentUser.DeviceId) || currentUser.DeviceId != deviceId)
            throw new HubException("Device identity mismatch.");
        return JoinAsync(HubChannels.PrintRequester(deviceId));
    }

    protected override async Task OnChannelsLeftAsync(IReadOnlyList<string> channels)
    {
        if (Context.Items.TryGetValue(DeviceKey, out var value) && value is string deviceId)
        {
            var now = DateTime.UtcNow;
            await db.PrintNodes.Where(x => x.DeviceId == deviceId)
                .ExecuteUpdateAsync(x => x.SetProperty(p => p.LastSeenAt, now), CancellationToken.None);
        }
    }
}
