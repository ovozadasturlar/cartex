using Cartex.Application.Common.Interfaces;
using Cartex.Application.Sms;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Api.Hubs;

[Authorize]
public sealed class SmsGatewayHub(IApplicationDbContext db, ICurrentUser currentUser, HubPresence presence)
    : PresenceHub(presence)
{
    private const string SlotPrefix = "sms-slot:";

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
        await JoinAsync(HubChannels.SmsGateway(deviceId, simSlot));
        Context.Items[SlotPrefix + simSlot] = device.Id;
    }

    protected override async Task OnChannelsLeftAsync(IReadOnlyList<string> channels)
    {
        var ids = Context.Items
            .Where(x => x.Key is string key && key.StartsWith(SlotPrefix, StringComparison.Ordinal))
            .Select(x => x.Value)
            .OfType<long>()
            .ToList();
        if (ids.Count == 0) return;
        var now = DateTime.UtcNow;
        await db.SmsGatewayDevices.Where(x => ids.Contains(x.Id))
            .ExecuteUpdateAsync(x => x.SetProperty(p => p.LastSeenAt, now), CancellationToken.None);
    }
}
