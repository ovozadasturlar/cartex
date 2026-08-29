namespace Cartex.Mobile.Store.Services;

public sealed record MobileSimInfo(int Slot, string Operator, string SubscriptionId, string DisplayName);

public interface ISmsGatewayPlatform
{
    Task<bool> EnsurePermissionAsync();
    Task<IReadOnlyList<MobileSimInfo>> GetSimsAsync();
    Task SendAsync(string subscriptionId, string phone, string text, long jobId,
        Func<Task> sent, Func<Task> delivered, CancellationToken cancellationToken);
}

public static class SmsForegroundControl
{
    public static Action? Start { get; set; }
    public static Action? Stop { get; set; }
}
