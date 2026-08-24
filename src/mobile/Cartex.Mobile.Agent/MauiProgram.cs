using Cartex.ApiClient;
using Cartex.Mobile.Agent.Data;
using Cartex.Mobile.Agent.Services;
using Cartex.Mobile.Agent.ViewModels;
using Cartex.Mobile.Agent.Views;
using BarcodeScanning;
using Cartex.Mobile.Core;

namespace Cartex.Mobile.Agent;

public static class MauiProgram
{
    private static int _handlingUnauthorized;

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
            ct => Resolve<MobileAuthService>().ForceRefreshAsync(ct),
            OnUnauthorized,
            "mobile",
            deviceIdProvider: () => MobileDeviceIdentity.DeviceId,
            deviceNameProvider: () => MobileDeviceIdentity.DeviceName);

        builder.Services.AddSingleton<MobileAuthService>();
        builder.Services.AddSingleton<AgentDb>();
        builder.Services.AddSingleton<SyncService>();
        builder.Services.AddSingleton<MobilePermissions>();
        builder.Services.AddSingleton<MobileFeaturesCache>();
        builder.Services.AddSingleton<SalesPolicyCache>();
        builder.Services.AddSingleton<MobileAccessStateLoader>();
        builder.Services.AddSingleton<IAccessStateLoader>(services => services.GetRequiredService<MobileAccessStateLoader>());
        builder.Services.AddSingleton<AccessState>();
        builder.Services.AddSingleton<ImageUrlBuilder>();
        builder.Services.AddSingleton<CartService>();
        builder.Services.AddTransient<CatalogViewModel>();
        builder.Services.AddTransient<CartViewModel>();
        builder.Services.AddTransient<ProductViewModel>();
        builder.Services.AddTransient<ProductEditViewModel>();
        builder.Services.AddTransient<CatalogPage>();
        builder.Services.AddTransient<CartPage>();
        builder.Services.AddTransient<ProductPage>();
        builder.Services.AddTransient<ProductEditPage>();
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
        builder.Services.AddTransient<DaySummaryViewModel>();
        builder.Services.AddSingleton<IBiometricAuth, BiometricAuth>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<ServerScanPage>();
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
        builder.Services.AddTransient<MapPickerPage>();
        builder.Services.AddTransient<CustomersMapPage>();
        builder.Services.AddTransient<RoutePage>();
        builder.Services.AddTransient<DaySummaryPage>();
        builder.Services.AddTransient<VanStockPage>();
        builder.Services.AddTransient<PinPage>();
        builder.Services.AddTransient<SecurityPage>();
        builder.Services.AddTransient<ScanPage>();
        builder.Services.AddTransient<OrdersPage>();
        builder.Services.AddTransient<OrderPage>();

        var app = builder.Build();
        app.Services.GetRequiredService<MobileAccessStateLoader>().StartConnectivityWatch();
        Cartex.Mobile.Core.Controls.Thumb.UrlBuilder = app.Services.GetRequiredService<ImageUrlBuilder>();
        var syncService = app.Services.GetRequiredService<SyncService>();
        syncService.StartConnectivityWatch();
        syncService.StartAutoSync();
        return app;
    }

    private static T Resolve<T>() where T : notnull =>
        IPlatformApplication.Current!.Services.GetRequiredService<T>();

    private static void OnUnauthorized()
    {
        if (Interlocked.Exchange(ref _handlingUnauthorized, 1) != 0) return;
        if (!SessionStore.HasSession)
        {
            Volatile.Write(ref _handlingUnauthorized, 0);
            return;
        }
        Resolve<SessionStore>().Clear();
        Resolve<AccessState>().Clear();
        MainThread.BeginInvokeOnMainThread(() => _ = NavigateToLoginAsync());
    }

    private static async Task NavigateToLoginAsync()
    {
        try
        {
            if (Shell.Current is { } shell
                && !shell.CurrentState.Location.OriginalString.Contains("login", StringComparison.Ordinal))
                await shell.GoToAsync("//login");
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(exception);
        }
        finally
        {
            Volatile.Write(ref _handlingUnauthorized, 0);
        }
    }
}
