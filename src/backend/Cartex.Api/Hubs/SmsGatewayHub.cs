using Cartex.Application.Sms;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Api.Hubs;

[Authorize]
public sealed class SmsGatewayHub(IApplicationDbContext db, ICurrentUser currentUser) : Hub
{
    public async Task Subscribe(string deviceId, int simSlot, string hostToken)
    {
        if (!currentUser.HasPermission(AppPermissions.SmsGateway.Host))
            throw new HubException("SMS gateway host permission is required.");
        if (currentUser.DeviceId != deviceId)
            throw new HubException("Device identity mismatch.");
        var device = await db.SmsGatewayDevices.FirstOrDefaultAsync(x => x.DeviceId == deviceId && x.SimSlot == simSlot
            && x.IsTrusted && x.IsConsented && x.IsEnabled);
        if (device is null)
            throw new HubException("SMS gateway is not active.");
        try
        {
            SmsGatewayCredential.Ensure(device, hostToken);
        }
        catch
        {
            throw new HubException("Invalid SMS gateway credential.");
        }
        await Groups.AddToGroupAsync(Context.ConnectionId, SignalRSmsGatewayNotifier.Group(deviceId, simSlot));
    }
}
