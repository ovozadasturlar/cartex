using System.Collections.Concurrent;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Telephony;
using AndroidX.Core.Content;
using Microsoft.Maui.ApplicationModel;

namespace Cartex.Mobile.Store.Platforms.Android;

public sealed class AndroidSmsGatewayPlatform : Services.ISmsGatewayPlatform
{
    public async Task<bool> EnsurePermissionAsync()
    {
        var sms = await Permissions.RequestAsync<SendSmsPermission>();
        var phone = await Permissions.RequestAsync<ReadPhoneStatePermission>();
        return sms == PermissionStatus.Granted && phone == PermissionStatus.Granted;
    }

    public Task<IReadOnlyList<Services.MobileSimInfo>> GetSimsAsync()
    {
        var context = global::Android.App.Application.Context;
        if (ContextCompat.CheckSelfPermission(context, global::Android.Manifest.Permission.ReadPhoneState) != Permission.Granted)
            return Task.FromResult<IReadOnlyList<Services.MobileSimInfo>>([]);
        var manager = context.GetSystemService(Context.TelephonySubscriptionService) as SubscriptionManager;
        var items = manager?.ActiveSubscriptionInfoList?.Select(x => new Services.MobileSimInfo(
            x.SimSlotIndex,
            x.CarrierName?.ToString() ?? string.Empty,
            x.SubscriptionId.ToString(),
            $"SIM {x.SimSlotIndex + 1} · {x.CarrierName}"))
            .OrderBy(x => x.Slot).ToList() ?? [];
        return Task.FromResult<IReadOnlyList<Services.MobileSimInfo>>(items);
    }

    public async Task SendAsync(string subscriptionId, string phone, string text, long jobId,
        Func<Task> sent, Func<Task> delivered, CancellationToken cancellationToken)
    {
        if (!int.TryParse(subscriptionId, out var id))
            throw new InvalidOperationException("SIM subscription ID noto'g'ri.");
        var context = global::Android.App.Application.Context;
        SmsManager? manager;
        if (OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            var service = context.GetSystemService("sms") as SmsManager;
            manager = service?.CreateForSubscriptionId(id);
        }
        else
        {
#pragma warning disable CA1422
            manager = SmsManager.GetSmsManagerForSubscriptionId(id);
#pragma warning restore CA1422
        }
        if (manager is null)
            throw new InvalidOperationException("SMS manager topilmadi.");
        var parts = manager.DivideMessage(text)
            ?? throw new InvalidOperationException("SMS qismlarga ajratilmadi.");
        var sentTasks = new List<Task>(parts.Count);
        var deliveredTasks = new List<Task>(parts.Count);
        var sentIntents = new List<PendingIntent>(parts.Count);
        var deliveredIntents = new List<PendingIntent>(parts.Count);
        for (var index = 0; index < parts.Count; index++)
        {
            var sentAction = $"uz.cartex.store.SMS_SENT.{jobId}.{index}";
            var deliveredAction = $"uz.cartex.store.SMS_DELIVERED.{jobId}.{index}";
            sentTasks.Add(SmsStatusReceiver.AwaitAsync(sentAction, cancellationToken));
            deliveredTasks.Add(SmsStatusReceiver.AwaitAsync(deliveredAction, cancellationToken));
            sentIntents.Add(IntentFor(sentAction, (int)(jobId % int.MaxValue) + index));
            deliveredIntents.Add(IntentFor(deliveredAction, (int)(jobId % int.MaxValue) + 1000 + index));
        }
        manager.SendMultipartTextMessage(phone, null, parts, sentIntents, deliveredIntents);
        await Task.WhenAll(sentTasks).WaitAsync(TimeSpan.FromMinutes(2), cancellationToken);
        await sent();
        await Task.WhenAll(deliveredTasks).WaitAsync(TimeSpan.FromMinutes(10), cancellationToken);
        await delivered();
    }

    private static PendingIntent IntentFor(string action, int requestCode)
    {
        var context = global::Android.App.Application.Context;
        var intent = new Intent(context, typeof(SmsStatusReceiver)).SetAction(action);
        return PendingIntent.GetBroadcast(context, requestCode, intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
    }
}

public sealed class SendSmsPermission : Permissions.BasePlatformPermission
{
    public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
        [(global::Android.Manifest.Permission.SendSms, true)];
}

public sealed class ReadPhoneStatePermission : Permissions.BasePlatformPermission
{
    public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
        [(global::Android.Manifest.Permission.ReadPhoneState, true)];
}

[BroadcastReceiver(Enabled = true, Exported = false)]
public sealed class SmsStatusReceiver : BroadcastReceiver
{
    private static readonly ConcurrentDictionary<string, TaskCompletionSource> Pending = new(StringComparer.Ordinal);

    public static Task AwaitAsync(string action, CancellationToken cancellationToken)
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!Pending.TryAdd(action, source))
            throw new InvalidOperationException("SMS callback takrorlandi.");
        cancellationToken.Register(() =>
        {
            if (Pending.TryRemove(action, out var pending))
                pending.TrySetCanceled(cancellationToken);
        });
        return source.Task;
    }

    public override void OnReceive(Context? context, Intent? intent)
    {
        var action = intent?.Action;
        if (action is null || !Pending.TryRemove(action, out var source))
            return;
        if (ResultCode == Result.Ok)
            source.TrySetResult();
        else
            source.TrySetException(new InvalidOperationException($"Android SMS natijasi: {(int)ResultCode}"));
    }
}
