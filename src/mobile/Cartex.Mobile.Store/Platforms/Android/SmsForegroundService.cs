using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;

namespace Cartex.Mobile.Store.Platforms.Android;

[Service(Exported = false, ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeDataSync)]
public sealed class SmsForegroundService : Service
{
    private const string ChannelId = "cartex_sms_gateway";
    private const int NotificationId = 4202;

    public static void Enable()
    {
        var context = global::Android.App.Application.Context;
        var intent = new Intent(context, typeof(SmsForegroundService));
        if (OperatingSystem.IsAndroidVersionAtLeast(26)) context.StartForegroundService(intent);
        else context.StartService(intent);
    }

    public static void Disable()
    {
        var context = global::Android.App.Application.Context;
        context.StopService(new Intent(context, typeof(SmsForegroundService)));
    }

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        StartForeground(NotificationId, BuildNotification());
        return StartCommandResult.Sticky;
    }

    private Notification BuildNotification()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(26)
            && GetSystemService(NotificationService) is NotificationManager manager)
        {
            manager.CreateNotificationChannel(new NotificationChannel(ChannelId,
                Loc.Instance["sms_gateway_title"], NotificationImportance.Low));
        }
        var builder = new NotificationCompat.Builder(this, ChannelId);
        builder.SetContentTitle(Loc.Instance["sms_gateway_title"]);
        builder.SetContentText(Loc.Instance["sms_gateway_running"]);
        builder.SetSmallIcon(global::Android.Resource.Drawable.StatSysUpload);
        builder.SetOngoing(true);
        builder.SetPriority((int)NotificationPriority.Low);
        return builder.Build()!;
    }
}

public static class SmsForegroundBootstrap
{
    public static void Register()
    {
        SmsForegroundControl.Start = SmsForegroundService.Enable;
        SmsForegroundControl.Stop = SmsForegroundService.Disable;
    }
}
