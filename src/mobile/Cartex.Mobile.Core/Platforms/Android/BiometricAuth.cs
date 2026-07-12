using AndroidX.Biometric;
using AndroidX.Core.Content;
using AndroidX.Fragment.App;

namespace Cartex.Mobile.Core;

public sealed class BiometricAuth : IBiometricAuth
{
    public bool IsAvailable =>
        Platform.CurrentActivity is { } activity &&
        BiometricManager.From(activity).CanAuthenticate(BiometricManager.Authenticators.BiometricWeak) == BiometricManager.BiometricSuccess;

    public Task<bool> AuthenticateAsync(string title)
    {
        var tcs = new TaskCompletionSource<bool>();
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (Platform.CurrentActivity is not FragmentActivity activity)
            {
                tcs.TrySetResult(false);
                return;
            }
            try
            {
                var prompt = new BiometricPrompt(activity, ContextCompat.GetMainExecutor(activity), new Callback(tcs));
                var info = new BiometricPrompt.PromptInfo.Builder()
                    .SetTitle(title)
                    .SetAllowedAuthenticators(BiometricManager.Authenticators.BiometricWeak)
                    .SetNegativeButtonText(Loc.Instance["cancel"])
                    .Build();
                prompt.Authenticate(info);
            }
            catch
            {
                tcs.TrySetResult(false);
            }
        });
        return tcs.Task;
    }

    private sealed class Callback(TaskCompletionSource<bool> tcs) : BiometricPrompt.AuthenticationCallback
    {
        public override void OnAuthenticationSucceeded(BiometricPrompt.AuthenticationResult result) => tcs.TrySetResult(true);
        public override void OnAuthenticationError(int errorCode, Java.Lang.ICharSequence errString) => tcs.TrySetResult(false);
    }
}
