using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Api.Hubs;

[Authorize]
public sealed class PrintingHub(IApplicationDbContext db, ICurrentUser currentUser) : Hub
{
    public async Task Subscribe(string deviceId, string hostToken)
    {
        if (!currentUser.HasPermission(AppPermissions.Printing.Host))
            throw new HubException("Print host permission is required.");
        if (!string.IsNullOrWhiteSpace(currentUser.DeviceId) && currentUser.DeviceId != deviceId)
            throw new HubException("Device identity mismatch.");
        var node = await db.PrintNodes.FirstOrDefaultAsync(x => x.DeviceId == deviceId
            && x.IsEnabled && x.HostEnabled && x.LastUserId == currentUser.UserId);
        if (node is null) throw new HubException("Print node is not registered.");
        try { Cartex.Application.Printing.PrintingCredential.Ensure(node, hostToken); }
        catch { throw new HubException("Invalid print host credential."); }
        await Groups.AddToGroupAsync(Context.ConnectionId, SignalRPrintJobNotifier.HostGroup(deviceId));
    }

    public Task SubscribeRequester(string deviceId)
    {
        if (!currentUser.HasPermission(AppPermissions.Printing.RemoteUse))
            throw new HubException("Remote printing permission is required.");
        if (string.IsNullOrWhiteSpace(currentUser.DeviceId) || currentUser.DeviceId != deviceId)
            throw new HubException("Device identity mismatch.");
        return Groups.AddToGroupAsync(Context.ConnectionId, SignalRPrintJobNotifier.RequesterGroup(deviceId));
    }
}
