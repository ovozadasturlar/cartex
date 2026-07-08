namespace Cartex.Mobile.Agent.Services;

public static class Ui
{
    public static void Toast(string message) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
#if ANDROID
            Android.Widget.Toast.MakeText(Android.App.Application.Context, message, Android.Widget.ToastLength.Short)?.Show();
#endif
        });
}
