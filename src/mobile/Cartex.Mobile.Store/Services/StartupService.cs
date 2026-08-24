using Cartex.Mobile.Core;

namespace Cartex.Mobile.Store.Services;

// Ishga tushishdagi kalit/baza ishlari fonda bajariladi — UI thread'da bajarilsa birinchi
// kadr shuncha kechikadi. Faqat navigatsiya UI thread'da qoladi.
public sealed class StartupService(
    MobileAuthService auth,
    AccessState access,
    MobileOfflineService offline,
    MobileHubHostService hubHost,
    SmsGatewayHostService smsGateway)
{
    public async Task RunAsync()
    {
        if (!await auth.TryRestoreAsync())
        {
            access.Clear();
            if (Shell.Current is { } current && !current.CurrentState.Location.OriginalString.Contains("login"))
                await current.GoToAsync("//login");
            return;
        }
        await access.EnsureLoadedAsync();
        _ = Task.Run(offline.StartAsync);
        hubHost.Start();
        _ = Task.Run(StartSmsGatewayAsync);
        _ = Task.Run(auth.ValidateSessionAsync);
        if (AppLock.PinEnabled)
            await Shell.Current.GoToAsync("pin", false);
    }

    private async Task StartSmsGatewayAsync()
    {
        try
        {
            await smsGateway.StartAsync();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(exception);
        }
    }
}
