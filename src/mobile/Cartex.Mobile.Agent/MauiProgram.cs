using Cartex.ApiClient;
using Cartex.Mobile.Agent.Data;
using Cartex.Mobile.Agent.Services;
using Cartex.Mobile.Agent.ViewModels;
using Cartex.Mobile.Agent.Views;
using Microsoft.Extensions.DependencyInjection;
using ZXing.Net.Maui.Controls;

namespace Cartex.Mobile.Agent;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseBarcodeReader()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
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
            "mobile");

        builder.Services.AddSingleton<MobileAuthService>();
        builder.Services.AddSingleton<AgentDb>();
        builder.Services.AddSingleton<SyncService>();
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<HomeViewModel>();
        builder.Services.AddTransient<CustomersViewModel>();
        builder.Services.AddTransient<CustomerViewModel>();
        builder.Services.AddTransient<SaleViewModel>();
        builder.Services.AddTransient<RepayViewModel>();
        builder.Services.AddTransient<TransfersViewModel>();
        builder.Services.AddTransient<OutboxViewModel>();
        builder.Services.AddTransient<ScanViewModel>();
        builder.Services.AddTransient<OrdersViewModel>();
        builder.Services.AddTransient<OrderViewModel>();
        builder.Services.AddTransient<ProfileViewModel>();
        builder.Services.AddTransient<ChangePasswordViewModel>();
        builder.Services.AddTransient<DevicesViewModel>();
        builder.Services.AddTransient<CustomerCreateViewModel>();
        builder.Services.AddTransient<VanStockViewModel>();
        builder.Services.AddTransient<PinViewModel>();
        builder.Services.AddTransient<SecurityViewModel>();
        builder.Services.AddSingleton<IBiometricAuth, Platforms.Android.BiometricAuth>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<CustomersPage>();
        builder.Services.AddTransient<CustomerPage>();
        builder.Services.AddTransient<SalePage>();
        builder.Services.AddTransient<RepayPage>();
        builder.Services.AddTransient<TransfersPage>();
        builder.Services.AddTransient<OutboxPage>();
        builder.Services.AddTransient<ProfilePage>();
        builder.Services.AddTransient<ChangePasswordPage>();
        builder.Services.AddTransient<DevicesPage>();
        builder.Services.AddTransient<CustomerCreatePage>();
        builder.Services.AddTransient<VanStockPage>();
        builder.Services.AddTransient<PinPage>();
        builder.Services.AddTransient<SecurityPage>();
        builder.Services.AddTransient<ScanPage>();
        builder.Services.AddTransient<OrdersPage>();
        builder.Services.AddTransient<OrderPage>();

        var app = builder.Build();
        app.Services.GetRequiredService<SyncService>().StartConnectivityWatch();
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
