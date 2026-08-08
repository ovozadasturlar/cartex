using Android.Content;
using Android.Views.InputMethods;
using Android.Widget;

namespace Cartex.Mobile.Store.Services;

public static class KeyboardDismissal
{
    public static void Hide()
    {
        var activity = Platform.CurrentActivity;
        var view = activity?.CurrentFocus;
        if (activity is null || view is null) return;

        var token = view.WindowToken;
        view.ClearFocus();
        var manager = activity.GetSystemService(Context.InputMethodService) as InputMethodManager;
        manager?.HideSoftInputFromWindow(token, HideSoftInputFlags.None);
    }

    public static void HideWhenInputIsNotFocused()
    {
        if (Platform.CurrentActivity?.CurrentFocus is EditText { HasFocus: true }) return;
        Hide();
    }
}
