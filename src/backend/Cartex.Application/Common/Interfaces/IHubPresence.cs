namespace Cartex.Application.Common.Interfaces;

public interface IHubPresence
{
    bool IsOnline(string channel);
}

public static class HubChannels
{
    public static string PrintHost(string deviceId) => $"print-host:{deviceId}";

    public static string PrintRequester(string deviceId) => $"print-requester:{deviceId}";

    public static string SmsGateway(string deviceId, int simSlot) => $"sms-gateway:{deviceId}:{simSlot}";

    public static string CartFeed(long branchId) => $"cart-feed:{branchId}";
}
