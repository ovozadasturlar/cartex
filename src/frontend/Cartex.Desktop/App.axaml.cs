using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Cartex.ApiClient.Api;
using Cartex.Desktop.Services;
using Cartex.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace Cartex.Desktop;

public partial class App : Application
{
    private static App? _current;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        _current = this;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        var provider = services.BuildServiceProvider();
        ServiceLocator.Initialize(provider);

        var settings = SettingsService.Instance;
        LocalizationManager.Instance.CurrentLanguage = settings.Language;
        ApplyTheme(settings.Theme);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var nav = provider.GetRequiredService<NavigationService>();
            var loginVm = provider.GetRequiredService<LoginViewModel>();

            var window = new MainWindow { DataContext = nav };
            nav.NavigateTo(loginVm);
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        var baseUrl = SettingsService.Instance.ApiBaseUrl;

        services.AddSingleton<NavigationService>();
        services.AddSingleton<AuthService>();

        services.AddRefitClient<IAuthApi>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl));
        services.AddRefitClient<IProductsApi>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl));
        services.AddRefitClient<ISalesApi>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl));
        services.AddRefitClient<ICustomersApi>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl));
        services.AddRefitClient<IWarehousesApi>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl));
        services.AddRefitClient<IStocksApi>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl));
        services.AddRefitClient<IUsersApi>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl));
        services.AddRefitClient<IRolesApi>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl));
        services.AddRefitClient<IPermissionsApi>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl));
        services.AddRefitClient<ICategoriesApi>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl));

        services.AddTransient<LoginViewModel>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<ProductsViewModel>();
        services.AddTransient<SalesViewModel>();
        services.AddTransient<CustomersViewModel>();
        services.AddTransient<WarehouseViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<UsersViewModel>();
        services.AddTransient<RolesViewModel>();
    }

    public static void ApplyTheme(string theme)
    {
        if (_current is null) return;
        _current.RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
    }
}