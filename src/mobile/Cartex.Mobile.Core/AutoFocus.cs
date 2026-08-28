using System.ComponentModel;

namespace Cartex.Mobile.Core;

// Oyna ochilishi bilan kursor birinchi maydonga tushadi: foydalanuvchi darhol yozishni
// boshlaydi. Faqat talab bo'yicha ochiladigan oynalarga qo'yiladi - doim ko'rinib turgan
// maydonga qo'yilsa klaviatura sahifa ochilishi bilan ekranni yopib qo'yardi.
public static class AutoFocus
{
    public static readonly BindableProperty OnVisibleProperty = BindableProperty.CreateAttached(
        "OnVisible", typeof(bool), typeof(AutoFocus), false, propertyChanged: OnChanged);

    public static bool GetOnVisible(BindableObject target) => (bool)target.GetValue(OnVisibleProperty);

    public static void SetOnVisible(BindableObject target, bool value) => target.SetValue(OnVisibleProperty, value);

    private static void OnChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not VisualElement element) return;
        element.PropertyChanged -= OnVisibilityChanged;
        if (newValue is true)
        {
            element.PropertyChanged += OnVisibilityChanged;
            if (element.IsVisible) _ = FocusAsync(element);
        }
    }

    private static void OnVisibilityChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(VisualElement.IsVisible) && sender is VisualElement { IsVisible: true } element)
            _ = FocusAsync(element);
    }

    private static async Task FocusAsync(VisualElement element)
    {
        // Oyna chizilib bo'lsin, aks holda fokus hali mavjud bo'lmagan maydonga tushadi.
        await Task.Delay(150);
        await element.Dispatcher.DispatchAsync(() =>
        {
            if (element.IsVisible) First(element)?.Focus();
        });
    }

    private static InputView? First(Element element)
    {
        foreach (var child in element.GetVisualTreeDescendants())
        {
            if (child is InputView { IsVisible: true, IsEnabled: true } input) return input;
        }
        return null;
    }
}
