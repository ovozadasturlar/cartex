using Microsoft.AspNetCore.SignalR.Client;

namespace Cartex.UI.Services;

/// Har bir hub klienti shu yerdan quriladi: token yangilash, qurilma sarlavhalari va
/// cheksiz qayta ulanish siyosati bitta joyda turadi. Standart `WithAutomaticReconnect()`
/// to'rt urinishdan keyin butunlay to'xtaydi va kanal jimgina o'lik qoladi.
public static class HubConnections
{
    public static HubConnection Create(string path, AuthService auth) =>
        new HubConnectionBuilder()
            .WithUrl(SettingsService.Instance.ApiBaseUrl.TrimEnd('/') + path, options =>
            {
                options.AccessTokenProvider = () => auth.EnsureFreshTokenAsync(CancellationToken.None);
                options.Headers["X-Client"] = "desktop";
                options.Headers["X-Device-Id"] = auth.DeviceId;
                options.Headers["X-Device-Name"] = auth.DeviceName;
            })
            .WithAutomaticReconnect(new EndlessRetryPolicy())
            .Build();

    private sealed class EndlessRetryPolicy : IRetryPolicy
    {
        private static readonly TimeSpan[] Steps =
            [TimeSpan.Zero, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5)];

        public TimeSpan? NextRetryDelay(RetryContext retryContext) =>
            Steps[(int)Math.Min(retryContext.PreviousRetryCount, Steps.Length - 1)];
    }
}
