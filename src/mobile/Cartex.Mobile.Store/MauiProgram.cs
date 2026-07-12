using BarcodeScanning;
using Cartex.ApiClient;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;
using Cartex.Mobile.Store.ViewModels;
using Cartex.Mobile.Store.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Cartex.Mobile.Store;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseBarcodeScanning()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("materialdesignicons.ttf", "MDI");
            });

#if ANDROID
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("NoUnderline", (handler, _) =>
            handler.PlatformView.BackgroundTintList =
                Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent));
#endif

        var session = new SessionStore();
        builder.Services.AddSingleton(session);
        builder.Services.AddApiClients(
            () => session.ServerUrl,
            () => session.AccessToken,
            ct => Resolve<MobileAuthService>().EnsureFreshTokenAsync(ct),
            OnUnauthorized,
            "store");

        builder.Services.AddSingleton<MobileAuthService>();
        builder.Services.AddSingleton<MobilePermissions>();
        builder.Services.AddSingleton<CartStore>();
        builder.Services.AddSingleton<WarehouseContext>();
        builder.Services.AddSingleton<IBiometricAuth, BiometricAuth>();

        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<PinViewModel>();
        builder.Services.AddTransient<SecurityViewModel>();
        builder.Services.AddTransient<ChangePasswordViewModel>();
        builder.Services.AddTransient<DevicesViewModel>();
        builder.Services.AddTransient<ProfileViewModel>();
        builder.Services.AddTransient<HomeViewModel>();
        builder.Services.AddTransient<ScanViewModel>();
        builder.Services.AddTransient<CartViewModel>();
        builder.Services.AddTransient<HandoffViewModel>();
        builder.Services.AddTransient<CheckoutViewModel>();
        builder.Services.AddTransient<QueueViewModel>();
        builder.Services.AddTransient<SalesViewModel>();

        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<PinPage>();
        builder.Services.AddTransient<SecurityPage>();
        builder.Services.AddTransient<ChangePasswordPage>();
        builder.Services.AddTransient<DevicesPage>();
        builder.Services.AddTransient<ProfilePage>();
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<ScanPage>();
        builder.Services.AddTransient<CartPage>();
        builder.Services.AddTransient<HandoffPage>();
        builder.Services.AddTransient<CheckoutPage>();
        builder.Services.AddTransient<QueuePage>();
        builder.Services.AddTransient<SalesPage>();

        return builder.Build();
    }

    private static T Resolve<T>() where T : notnull =>
        IPlatformApplication.Current!.Services.GetRequiredService<T>();

    private static void OnUnauthorized()
    {
        Resolve<SessionStore>().Clear();
        MainThread.BeginInvokeOnMainThread(() => _ = Shell.Current.GoToAsync("//login"));
    }
}
