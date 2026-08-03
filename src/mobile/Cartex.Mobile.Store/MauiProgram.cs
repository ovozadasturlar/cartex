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
            "store",
            TimeSpan.FromSeconds(20),
            () => MobileDeviceIdentity.DeviceId,
            () => MobileDeviceIdentity.DeviceName);

        builder.Services.AddSingleton<MobileAuthService>();
        builder.Services.AddSingleton<MobilePermissions>();
        builder.Services.AddSingleton<AppCapabilities>();
        builder.Services.AddSingleton<ImageUrlBuilder>();
        builder.Services.AddSingleton<CartStore>();
        builder.Services.AddSingleton<SupplyCartStore>();
        builder.Services.AddSingleton<WarehouseContext>();
        builder.Services.AddSingleton<OrderingHubService>();
        builder.Services.AddSingleton<MobilePrintDispatcher>();
        builder.Services.AddSingleton<BarcodeLabelSettingsCache>();
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
        builder.Services.AddTransient<TradeViewModel>();
        builder.Services.AddTransient<CustomersViewModel>();
        builder.Services.AddTransient<ReceiveCartViewModel>();
        builder.Services.AddTransient<ProductEditViewModel>();
        builder.Services.AddTransient<BarcodeAttachViewModel>();

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
        builder.Services.AddTransient<TradePage>();
        builder.Services.AddTransient<CustomersPage>();
        builder.Services.AddTransient<ReceiveCartPage>();
        builder.Services.AddTransient<ProductEditPage>();
        builder.Services.AddTransient<BarcodeAttachPage>();

        var app = builder.Build();
        Cartex.Mobile.Core.Controls.Thumb.UrlBuilder = app.Services.GetRequiredService<ImageUrlBuilder>();
        Cartex.Mobile.Core.Controls.Thumb.PublicBaseUrl = session.ServerUrl;
        Cartex.Mobile.Core.Controls.Thumb.AccessTokenProvider =
            ct => app.Services.GetRequiredService<MobileAuthService>().EnsureFreshTokenAsync(ct);
        return app;
    }

    private static T Resolve<T>() where T : notnull =>
        IPlatformApplication.Current!.Services.GetRequiredService<T>();

    private static void OnUnauthorized()
    {
        Resolve<SessionStore>().Clear();
        MainThread.BeginInvokeOnMainThread(() => _ = Shell.Current.GoToAsync("//login"));
    }
}
