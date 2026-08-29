using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;

namespace Cartex.Mobile.Store.Platforms.Android;

// HUB-01: telefon HUB bo'lganda tinglovchi ekran o'chgach ham ishlashi kerak. Android buni
// faqat ko'rinadigan bildirishnomali fon xizmati bilan kafolatlaydi; Wi-Fi qulfi esa
// uyqu rejimida tarmoq adapteri o'chib qolmasligi uchun.
[Service(Exported = false, ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeDataSync)]
public sealed class HubForegroundService : Service
{
    private const string ChannelId = "cartex_hub";
    private const int NotificationId = 4201;
    private global::Android.Net.Wifi.WifiManager.WifiLock? _wifiLock;

    public static void Enable()
    {
        var context = global::Android.App.Application.Context;
        var intent = new Intent(context, typeof(HubForegroundService));
        if (OperatingSystem.IsAndroidVersionAtLeast(26)) context.StartForegroundService(intent);
        else context.StartService(intent);
    }

    public static void Disable()
    {
        var context = global::Android.App.Application.Context;
        context.StopService(new Intent(context, typeof(HubForegroundService)));
    }

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        StartForeground(NotificationId, BuildNotification());
        AcquireWifiLock();
        return StartCommandResult.Sticky;
    }

    public override void OnDestroy()
    {
        if (_wifiLock is { IsHeld: true }) _wifiLock.Release();
        _wifiLock = null;
        base.OnDestroy();
    }

    private void AcquireWifiLock()
    {
        // Android 10 dan boshlab qulf eskirgan deb belgilangan: fon xizmati ishlab turganda
        // adapterni tizimning o'zi ushlab turadi. Undan pastda esa usiz uyquda uzilib qoladi.
        if (_wifiLock is not null || OperatingSystem.IsAndroidVersionAtLeast(29)) return;
        if (GetSystemService(WifiService) is not global::Android.Net.Wifi.WifiManager manager) return;
        _wifiLock = manager.CreateWifiLock("cartex-hub");
        _wifiLock?.Acquire();
    }

    private Notification BuildNotification()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(26)
            && GetSystemService(NotificationService) is NotificationManager manager)
        {
            var channel = new NotificationChannel(ChannelId, Loc.Instance["hub_host_enable"],
                NotificationImportance.Low);
            manager.CreateNotificationChannel(channel);
        }

        var builder = new NotificationCompat.Builder(this, ChannelId);
        builder.SetContentTitle(Loc.Instance["hub_host_enable"]);
        builder.SetContentText(Loc.Instance["hub_serving"]);
        builder.SetSmallIcon(global::Android.Resource.Drawable.StatSysDownload);
        builder.SetOngoing(true);
        builder.SetPriority((int)NotificationPriority.Low);
        return builder.Build()!;
    }
}

public static class HubForegroundBootstrap
{
    public static void Register()
    {
        HubForegroundControl.Start = HubForegroundService.Enable;
        HubForegroundControl.Stop = HubForegroundService.Disable;
    }
}
