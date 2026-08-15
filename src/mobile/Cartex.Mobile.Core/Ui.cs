namespace Cartex.Mobile.Core;

public static class Ui
{
    private const int PulseMs = 120;
    private const int GapMs = 70;

    public static void Haptic()
    {
        try
        {
            HapticFeedback.Default.Perform(HapticFeedbackType.Click);
        }
        catch
        {
        }
    }

    public static void Vibrate(int pulses = 1)
    {
        if (pulses < 1) return;
#if ANDROID
        // Butun naqsh bitta waveform sifatida beriladi: pulslar orasidagi pauzani OS
        // millisekund aniqlikda boshqaradi; Task.Delay + alohida chaqiriqlarda motorning
        // to'xtash/yurgizilish vaqti qo'shilib pauza sezilarli cho'zilib ketadi.
        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                var timings = new long[pulses * 2];
                for (var i = 0; i < pulses; i++)
                {
                    timings[i * 2] = i == 0 ? 0 : GapMs;
                    timings[i * 2 + 1] = PulseMs;
                }
                var effect = Android.OS.VibrationEffect.CreateWaveform(timings, -1);
                if (effect is not null)
                {
                    AndroidVibrator()?.Vibrate(effect);
                    return;
                }
            }
        }
        catch
        {
        }
#endif
        LoopVibrate(pulses);
    }

    private static void LoopVibrate(int pulses)
    {
        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            for (var i = 0; i < pulses; i++)
            {
                if (i > 0) await Task.Delay(PulseMs + GapMs);
                try
                {
                    Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(PulseMs));
                }
                catch
                {
                }
            }
        });
    }

#if ANDROID
    private static Android.OS.Vibrator? AndroidVibrator()
    {
        var context = Android.App.Application.Context;
        if (OperatingSystem.IsAndroidVersionAtLeast(31))
            return ((Android.OS.VibratorManager?)context.GetSystemService(
                Android.Content.Context.VibratorManagerService))?.DefaultVibrator;
        return (Android.OS.Vibrator?)context.GetSystemService(Android.Content.Context.VibratorService);
    }
#endif

    public static void Toast(string message) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
#if ANDROID
            Android.Widget.Toast.MakeText(Android.App.Application.Context, message, Android.Widget.ToastLength.Short)?.Show();
#endif
        });
}
