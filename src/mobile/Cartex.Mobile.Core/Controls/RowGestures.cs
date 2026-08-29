using System.Windows.Input;

namespace Cartex.Mobile.Core.Controls;

// MAUI'da long-press jesti yo'q, TapGestureRecognizer bilan yonma-yon esa native
// long-press ishlamay qoladi. Shu sabab tap ham, long-press ham Android'ning o'z
// Click/LongClick mexanizmidan olinadi — u long-press'dan keyingi click'ni o'zi yutadi.
public static class RowGestures
{
    public static readonly BindableProperty TapCommandProperty = BindableProperty.CreateAttached(
        "TapCommand", typeof(ICommand), typeof(RowGestures), null, propertyChanged: OnCommandChanged);

    public static readonly BindableProperty LongPressCommandProperty = BindableProperty.CreateAttached(
        "LongPressCommand", typeof(ICommand), typeof(RowGestures), null, propertyChanged: OnCommandChanged);

    public static readonly BindableProperty CommandParameterProperty = BindableProperty.CreateAttached(
        "CommandParameter", typeof(object), typeof(RowGestures), null);

    private static readonly BindableProperty HookedViewProperty = BindableProperty.CreateAttached(
        "HookedView", typeof(object), typeof(RowGestures), null);

    public static ICommand? GetTapCommand(BindableObject view) => (ICommand?)view.GetValue(TapCommandProperty);
    public static void SetTapCommand(BindableObject view, ICommand? value) => view.SetValue(TapCommandProperty, value);
    public static ICommand? GetLongPressCommand(BindableObject view) => (ICommand?)view.GetValue(LongPressCommandProperty);
    public static void SetLongPressCommand(BindableObject view, ICommand? value) => view.SetValue(LongPressCommandProperty, value);
    public static object? GetCommandParameter(BindableObject view) => view.GetValue(CommandParameterProperty);
    public static void SetCommandParameter(BindableObject view, object? value) => view.SetValue(CommandParameterProperty, value);

    private static void OnCommandChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not View view) return;
        view.HandlerChanged -= OnHandlerChanged;
        view.HandlerChanged += OnHandlerChanged;
        Attach(view);
    }

    private static void OnHandlerChanged(object? sender, EventArgs e)
    {
        if (sender is View view) Attach(view);
    }

    private static void Attach(View view)
    {
        if (view.Handler?.PlatformView is not Android.Views.View platform) return;
        if (ReferenceEquals(view.GetValue(HookedViewProperty), platform)) return;
        view.SetValue(HookedViewProperty, platform);

        platform.Clickable = true;
        platform.LongClickable = true;
        platform.Click += (_, _) => Execute(view, TapCommandProperty);
        platform.LongClick += (_, e) => e.Handled = Execute(view, LongPressCommandProperty);
    }

    private static bool Execute(View view, BindableProperty commandProperty)
    {
        var command = (ICommand?)view.GetValue(commandProperty);
        var parameter = view.GetValue(CommandParameterProperty);
        if (command?.CanExecute(parameter) != true) return false;
        command.Execute(parameter);
        return true;
    }
}
