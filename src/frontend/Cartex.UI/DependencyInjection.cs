using Cartex.ApiClient;
using Cartex.UI.Services;
using Cartex.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Cartex.UI;

public static class DependencyInjection
{
    public static void RegisterServices(IServiceCollection services, SettingsService settings)
    {
        services.AddApiClients(settings.ApiBaseUrl);

        services.AddSingleton<AuthService>();
        services.AddSingleton<NavigationService>();
        services.AddSingleton(LocalizationManager.Instance);

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
}
