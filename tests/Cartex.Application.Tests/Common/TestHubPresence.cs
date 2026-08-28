using Cartex.Application.Common.Interfaces;

namespace Cartex.Application.Tests.Common;

public sealed class TestHubPresence : IHubPresence
{
    private readonly HashSet<string> _offline = new(StringComparer.Ordinal);

    public bool IsOnline(string channel) => !_offline.Contains(channel);

    public void PrintHostOffline(string deviceId) => _offline.Add(HubChannels.PrintHost(deviceId));

    public void SmsGatewayOffline(string deviceId, int simSlot) =>
        _offline.Add(HubChannels.SmsGateway(deviceId, simSlot));

    public void Reset() => _offline.Clear();
}
