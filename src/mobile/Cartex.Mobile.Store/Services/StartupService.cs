using Cartex.Mobile.Core;

namespace Cartex.Mobile.Store.Services;

// Ishga tushishdagi kalit/baza ishlari fonda bajariladi — UI thread'da bajarilsa birinchi
// kadr shuncha kechikadi. Faqat navigatsiya UI thread'da qoladi.
public sealed class StartupService(MobileAuthService auth, MobileOfflineService offline, MobileHubHostService hubHost)
{
    public async Task RunAsync()
    {
        if (!await auth.TryRestoreAsync())
        {
            if (Shell.Current is { } current && !current.CurrentState.Location.OriginalString.Contains("login"))
                await current.GoToAsync("//login");
            return;
        }
        _ = Task.Run(offline.StartAsync);
        hubHost.Start();
        _ = Task.Run(auth.ValidateSessionAsync);
        if (AppLock.PinEnabled)
            await Shell.Current.GoToAsync("pin", false);
    }
}
