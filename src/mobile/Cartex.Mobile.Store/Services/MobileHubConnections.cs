using Cartex.Mobile.Core;
using Microsoft.AspNetCore.SignalR.Client;

namespace Cartex.Mobile.Store.Services;

// Telefondagi hub klientlari shu yerdan quriladi. Token har ulanishda yangilanadi -
// aks holda uzoq ishlaydigan host eski token bilan qayta ulana olmay qolardi. Qayta
// ulanish cheksiz: standart siyosat to'rt urinishdan keyin butunlay to'xtaydi.
public static class MobileHubConnections
{
    public static HubConnection Create(string path, SessionStore session, MobileAuthService auth) =>
        new HubConnectionBuilder()
            .WithUrl(session.ServerUrl.TrimEnd('/') + path, options =>
            {
                options.AccessTokenProvider = () => auth.EnsureFreshTokenAsync(CancellationToken.None);
                options.Headers["X-Client"] = "store";
                options.Headers["X-Device-Id"] = auth.DeviceId;
            })
            .WithAutomaticReconnect(new EndlessRetryPolicy())
            .Build();

    private sealed class EndlessRetryPolicy : IRetryPolicy
    {
        private static readonly TimeSpan[] Steps =
            [TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)];

        public TimeSpan? NextRetryDelay(RetryContext retryContext) =>
            Steps[(int)Math.Min(retryContext.PreviousRetryCount, Steps.Length - 1)];
    }
}
